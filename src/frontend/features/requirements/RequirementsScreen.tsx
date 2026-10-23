import { Check, Copy, ExternalLink } from "lucide-react";
import { useEffect, useState } from "react";
import { useLocation } from "react-router";
import { AddFolderDialog } from "@/features/settings/AddFolderDialog";
import { explain } from "@/shared/http/client";
import { useFolders } from "@/shared/http/queries";
import {
  type MachineRequirement,
  usePreferences,
  useSetRequirementsSeen,
  useTools,
} from "@/shared/http/settings";
import { t, type TranslationKey } from "@/shared/i18n";
import { Badge, Button, Callout, ErrorState, Loading } from "@/shared/ui/kit";

/**
 * Requirements (#680): what this machine needs for the loop, each row present, missing or signed
 * out, and when it is not present the command that puts it right here, with a button that copies
 * it. Nothing is installed from this screen. It opens on start until the required rows are present
 * (`requirementsSeen` in the preferences, `app/useFirstStart.ts`), and from Settings always; on the
 * first start with everything required, it leads to adding the first folder.
 */
export function RequirementsScreen() {
  const tools = useTools();
  const preferences = usePreferences();
  const folders = useFolders();
  const seen = useSetRequirementsSeen();
  const firstStart = (useLocation().state as { firstStart?: boolean } | null)?.firstStart === true;
  const [adding, setAdding] = useState(false);
  const ready = tools.data?.ready === true;
  const noFolder = folders.data !== undefined && folders.data.length === 0;

  // Seen once every required row is present, so the next start opens where the person left.
  const unseen = preferences.data?.requirementsSeen === false;
  const { mutate: markSeen, isIdle } = seen;
  useEffect(() => {
    if (ready && unseen && isIdle) markSeen();
  }, [ready, unseen, isIdle, markSeen]);

  // The first start that finds everything here goes on to the first folder.
  const [offered, setOffered] = useState(false);
  useEffect(() => {
    if (firstStart && ready && noFolder && !offered) {
      setOffered(true);
      setAdding(true);
    }
  }, [firstStart, ready, noFolder, offered]);

  const rows = tools.data?.requirements ?? [];
  const required = rows.filter((row) => row.required);
  const optional = rows.filter((row) => !row.required);
  const missing =
    required.filter((row) => !row.agent && row.state !== "present").length +
    (required.some((row) => row.agent) && !required.some((row) => row.agent && row.state === "present") ? 1 : 0);

  return (
    <div
      style={{
        padding: "24px clamp(16px, 4vw, 40px) 40px",
        display: "flex",
        flexDirection: "column",
        gap: 24,
        maxWidth: 820,
      }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
        <h1 style={{ margin: 0, fontSize: "var(--text-lg)", fontWeight: 600 }}>
          {t("screen.requirements")}
        </h1>
        <p style={note}>{t("requirements.summary")}</p>
      </div>

      {tools.isPending ? <Loading /> : null}
      {tools.error ? (
        <ErrorState
          detail={t("requirements.failed", { reason: explain(tools.error) })}
          onRetry={() => void tools.refetch()}
        />
      ) : null}

      {tools.data ? (
        <>
          <Callout tone={ready ? "neutral" : "info"}>
            <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 12, flex: 1 }}>
              <div style={{ display: "flex", flexDirection: "column", gap: 2, flex: "1 1 240px" }}>
                <span style={{ fontWeight: 600 }}>
                  {ready
                    ? t("requirements.ready")
                    : t("requirements.notReady", { count: String(missing) })}
                </span>
                {ready && noFolder ? <span style={note}>{t("requirements.readyNext")}</span> : null}
              </div>
              {ready && noFolder ? (
                <Button size="sm" onClick={() => setAdding(true)}>
                  {t("rail.addFolder")}
                </Button>
              ) : (
                <Button
                  variant="outline"
                  size="sm"
                  disabled={tools.isFetching}
                  onClick={() => void tools.refetch()}
                >
                  {tools.isFetching ? t("requirements.checking") : t("requirements.check")}
                </Button>
              )}
            </div>
          </Callout>

          <Group title={t("requirements.required")} hint={t("requirements.requiredHint")} rows={required} />
          <Group title={t("requirements.optional")} hint={t("requirements.optionalHint")} rows={optional} />
        </>
      ) : null}

      {adding ? <AddFolderDialog onClose={() => setAdding(false)} /> : null}
    </div>
  );
}

function Group({ title, hint, rows }: { title: string; hint: string; rows: MachineRequirement[] }) {
  if (rows.length === 0) return null;
  // Another agent CLI being present, a missing one is a choice, not a gap.
  const agentPresent = rows.some((row) => row.agent && row.state === "present");
  return (
    <section aria-label={title} style={{ display: "flex", flexDirection: "column", gap: 8, minWidth: 0 }}>
      <div style={{ display: "flex", alignItems: "baseline", justifyContent: "space-between", gap: 12, flexWrap: "wrap" }}>
        <h2 style={sectionTitle}>{title}</h2>
        <span style={note}>{hint}</span>
      </div>
      <div style={list}>
        {rows.map((row, index) => (
          <Row key={row.name} row={row} last={index === rows.length - 1} needed={row.required && !(row.agent && agentPresent)} />
        ))}
      </div>
    </section>
  );
}

const purposes: Record<string, TranslationKey> = {
  git: "requirements.for.git",
  gh: "requirements.for.gh",
  node: "requirements.for.node",
  claude: "requirements.for.claude",
  opencode: "requirements.for.opencode",
  copilot: "requirements.for.copilot",
  sbx: "requirements.for.sbx",
  podman: "requirements.for.containers",
  docker: "requirements.for.containers",
};

function Row({ row, last, needed }: { row: MachineRequirement; last: boolean; needed: boolean }) {
  const purpose = purposes[row.name];
  const version = row.version ?? t("requirements.noVersion");
  const present = row.state === "present";
  const detail =
    row.state === "missing" && row.problem === null
      ? t("requirements.notOnPath")
      : !present
        ? (row.problem ?? "")
        : row.account
          ? t("requirements.signedInAs", { version, account: row.account })
          : t("requirements.version", { version });

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        gap: 8,
        padding: "10px 14px",
        borderBottom: last ? 0 : "1px solid var(--border)",
        minWidth: 0,
      }}
    >
      <div style={{ display: "flex", alignItems: "flex-start", gap: 12 }}>
        <div style={{ display: "flex", flexDirection: "column", gap: 2, minWidth: 0, flex: 1 }}>
          <span style={{ fontFamily: "var(--font-mono)" }}>{row.name}</span>
          {purpose ? <span style={note}>{t(purpose)}</span> : null}
          <span style={{ ...note, color: present ? "var(--muted-foreground)" : "var(--foreground)", overflowWrap: "anywhere" }}>
            {detail}
          </span>
        </div>
        <span style={{ flex: "none" }}>
          <Badge variant={present ? "success" : needed ? "warning" : "secondary"}>
            {present
              ? t("requirements.present")
              : row.state === "signed-out"
                ? t("requirements.signedOut")
                : t("requirements.missing")}
          </Badge>
        </span>
      </div>
      {!present && row.command ? <CopyCommand command={row.command} /> : null}
      {!present && row.link ? (
        <a href={row.link} target="_blank" rel="noreferrer" style={link}>
          {t("requirements.instructions")}
          <ExternalLink size={12} aria-hidden />
        </a>
      ) : null}
    </div>
  );
}

/** A command a person runs in their own terminal, whole however narrow the page, and a button that copies it. */
function CopyCommand({ command }: { command: string }) {
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!copied) return;
    const timer = setTimeout(() => setCopied(false), 2000);
    return () => clearTimeout(timer);
  }, [copied]);

  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8, minWidth: 0 }}>
      <code style={code}>{command}</code>
      <Button
        variant="outline"
        size="xs"
        aria-label={t("requirements.copyLabel", { command })}
        onClick={() => {
          void navigator.clipboard.writeText(command).then(() => setCopied(true));
        }}
      >
        {copied ? <Check size={12} aria-hidden /> : <Copy size={12} aria-hidden />}
        {copied ? t("requirements.copied") : t("requirements.copy")}
      </Button>
    </div>
  );
}

const note: React.CSSProperties = {
  margin: 0,
  fontSize: "var(--text-xs)",
  color: "var(--muted-foreground)",
};

const sectionTitle: React.CSSProperties = {
  ...note,
  fontWeight: 600,
  textTransform: "uppercase",
  letterSpacing: "0.04em",
};

const list: React.CSSProperties = {
  display: "flex",
  flexDirection: "column",
  border: "1px solid var(--border)",
  borderRadius: "var(--radius-xl)",
  overflow: "hidden",
};

const code: React.CSSProperties = {
  flex: 1,
  minWidth: 0,
  fontSize: "var(--text-xs)",
  fontFamily: "var(--font-mono)",
  overflowWrap: "anywhere",
  padding: "4px 8px",
  border: "1px solid var(--border)",
  borderRadius: "var(--radius-md)",
  background: "var(--muted)",
};

const link: React.CSSProperties = {
  display: "inline-flex",
  alignItems: "center",
  gap: 4,
  alignSelf: "flex-start",
  fontSize: "var(--text-xs)",
  color: "var(--primary)",
};
