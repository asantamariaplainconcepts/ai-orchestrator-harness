import { useMutation, useQuery } from "@tanstack/react-query";
import { Check, Copy } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { explain } from "@/shared/http/client";
import { updates } from "@/shared/http/api";
import { t } from "@/shared/i18n";
import { Button } from "@/shared/ui/kit";

/** What a person types to update the installed tool (#679). */
const updateCommand = "dotnet tool update -g aio";

/**
 * Updates (#679, ADR-0009, ADR-0010): the installed version, the newest on nuget.org, and the command
 * that updates. For an installed tool with a newer version, *Update and restart* runs that command
 * here (#703), shows what it printed and, when it installed, waits for the new version to answer and
 * reloads; the command stays below it for the person who would rather type it. The next `dnx aio`
 * takes the new version on its own.
 *
 * The tool asks nuget.org once when it starts; opening Settings reads that answer and asks nothing
 * new. *Check for updates* asks again. Every answer is a sentence, up to date or not.
 */
export function UpdateSection() {
  const status = useQuery({
    queryKey: ["updates"],
    queryFn: updates.status,
    retry: false,
    // No refetch on focus or interval: both would be a scheduled check, and there is none.
    refetchOnWindowFocus: false,
    staleTime: Infinity,
  });

  const check = useMutation({ mutationFn: updates.check });

  const answer = check.data ?? status.data ?? null;
  const failure =
    answer?.state === "error"
      ? (answer.detail ?? "")
      : check.isError
        ? ((check.error as Error)?.message ?? "")
        : null;

  return (
    <section style={{ display: "flex", flexDirection: "column", gap: 8 }}>
      <div style={{ fontSize: "var(--text-sm)", fontWeight: 500 }}>{t("settings.updates.title")}</div>

      <div role="status" style={muted}>
        {status.isPending
          ? `${t("settings.updates.checking")}…`
          : t("settings.updates.version", { version: answer?.installed ?? "" })}
      </div>

      {answer?.state === "up-to-date" ? (
        <div style={muted}>{t("settings.updates.upToDate", { version: answer.latest ?? "" })}</div>
      ) : null}

      {answer?.state === "unpublished" ? <div style={muted}>{t("settings.updates.unpublished")}</div> : null}

      {answer?.state === "available" ? (
        <>
          <div style={muted}>{t("settings.updates.available", { version: answer.latest ?? "" })}</div>
          {answer.how === "tool" ? (
            <>
              <Install installed={answer.installed} />
              <CopyableCommand command={updateCommand} />
            </>
          ) : answer.how === "dnx" ? (
            <div style={muted}>{t("settings.updates.dnx")}</div>
          ) : (
            <div style={muted}>{t("settings.updates.checkout")}</div>
          )}
        </>
      ) : null}

      {failure !== null ? <div style={muted}>{t("settings.updates.error", { reason: failure })}</div> : null}

      <div>
        <Button variant="outline" size="sm" disabled={check.isPending} onClick={() => check.mutate()}>
          {check.isPending ? t("settings.updates.checking") : t("settings.updates.check")}
        </Button>
      </div>
    </section>
  );
}

/**
 * The button, what the command printed, and the wait for the restart: the old server stops half a
 * second after it answers, and the page reloads once `/api/version` names another version.
 */
function Install({ installed }: { installed: string }) {
  const install = useMutation({ mutationFn: updates.install });
  const restarting = install.data?.succeeded === true;
  const answering = useQuery({
    queryKey: ["updates", "restart"],
    queryFn: updates.version,
    enabled: restarting,
    retry: false,
    refetchInterval: 2000,
  });

  const now = answering.data?.version?.split("+")[0];
  useEffect(() => {
    if (restarting && now && now !== installed) window.location.reload();
  }, [restarting, now, installed]);

  return (
    <>
      {restarting ? null : (
        <div>
          <Button size="sm" disabled={install.isPending} onClick={() => install.mutate()}>
            {install.isPending ? t("settings.updates.installing") : t("settings.updates.install")}
          </Button>
        </div>
      )}
      {install.data ? (
        <pre
          aria-label={t("settings.updates.install")}
          style={{
            margin: 0,
            maxHeight: 160,
            overflow: "auto",
            padding: "6px 10px",
            border: "1px solid var(--border)",
            borderRadius: "var(--radius-md)",
            background: "var(--muted)",
            fontFamily: "var(--font-mono)",
            fontSize: "var(--text-xs)",
            whiteSpace: "pre-wrap",
            overflowWrap: "anywhere",
          }}
        >
          {install.data.output}
        </pre>
      ) : null}
      {restarting ? (
        <div role="status" style={muted}>
          {t("settings.updates.restarting")}
        </div>
      ) : null}
      {install.data && !install.data.succeeded ? <div style={muted}>{t("settings.updates.installFailed")}</div> : null}
      {install.error ? (
        <div role="alert" style={{ ...muted, color: "var(--destructive)" }}>
          {explain(install.error)}
        </div>
      ) : null}
      <div style={muted}>{t("settings.updates.byHand")}</div>
    </>
  );
}

function CopyableCommand({ command }: { command: string }) {
  const [copied, setCopied] = useState(false);
  const code = useRef<HTMLElement>(null);

  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 8,
        minWidth: 0,
        padding: "4px 4px 4px 10px",
        border: "1px solid var(--border)",
        borderRadius: "var(--radius-md)",
        background: "var(--muted)",
      }}
    >
      <code
        ref={code}
        style={{
          flex: 1,
          minWidth: 0,
          fontFamily: "var(--font-mono)",
          fontSize: "var(--text-xs)",
          overflowWrap: "anywhere",
        }}
      >
        {command}
      </code>
      <Button
        variant="ghost"
        size="xs"
        aria-label={copied ? t("settings.updates.copied") : t("settings.updates.copy")}
        onClick={() =>
          void navigator.clipboard.writeText(command).then(
            () => {
              setCopied(true);
              window.setTimeout(() => setCopied(false), 1500);
            },
            // A browser that refuses the clipboard: the command is selected, one ⌘C away.
            () => {
              if (code.current) window.getSelection()?.selectAllChildren(code.current);
            },
          )
        }
        style={{ display: "inline-flex", alignItems: "center", gap: 4, flex: "none" }}
      >
        {copied ? <Check size={13} aria-hidden /> : <Copy size={13} aria-hidden />}
        {copied ? t("settings.updates.copied") : t("settings.updates.copy")}
      </Button>
    </div>
  );
}

const muted: React.CSSProperties = { fontSize: "var(--text-xs)", color: "var(--muted-foreground)" };
