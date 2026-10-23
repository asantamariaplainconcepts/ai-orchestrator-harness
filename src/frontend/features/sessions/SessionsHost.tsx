import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { explain } from "@/shared/http/client";
import { type MachineTool, useTools } from "@/shared/http/settings";
import { openSession, type SessionPlace, useOpenSession } from "@/shared/http/sessions";
import { t } from "@/shared/i18n";
import { Button, Dialog, ErrorState, Field, Loading, Select } from "@/shared/ui/kit";
import type { SessionAsk } from "./ask";

/**
 * Answers what a person asked to open by hand (#516, DESIGN.md#the-screens): Terminal opens the
 * login shell at once, with no dialog; Explore with agent asks for the agent, its model and an
 * optional command first. Either way the session goes to the tray and its terminal opens over the
 * screen, which `onOpened` asks the shell to do.
 *
 * Loaded the first time somebody asks, so none of it is the first screen's to pay for.
 */
export function SessionsHost({
  ask,
  onOpened,
  onClose,
}: {
  ask: SessionAsk;
  onOpened: (runId: string) => void;
  onClose: () => void;
}) {
  return ask.kind === "terminal" ? (
    <Terminal ask={ask} onOpened={onOpened} onClose={onClose} />
  ) : (
    <Explore ask={ask} onOpened={onOpened} onClose={onClose} />
  );
}

/**
 * Hands the new session to the shell and closes, once the form's mutation has an answer.
 */
function useOpened(
  opened: { runId: string } | undefined,
  onOpened: (runId: string) => void,
  onClose: () => void,
) {
  useEffect(() => {
    if (!opened) return;
    onOpened(opened.runId);
    onClose();
  }, [opened, onOpened, onClose]);
}

function placeOf(ask: SessionAsk): SessionPlace {
  return ask.pbi
    ? { kind: "pbi", repository: ask.pbi.repository, id: ask.pbi.id }
    : { kind: "folder", path: ask.folder.path };
}

/** The shell, opened as soon as it is asked for. Only a refusal shows anything. */
function Terminal({
  ask,
  onOpened,
  onClose,
}: {
  ask: SessionAsk;
  onOpened: (runId: string) => void;
  onClose: () => void;
}) {
  const client = useQueryClient();
  const [failure, setFailure] = useState<unknown>(null);
  const asked = useRef(false);

  // A plain promise, not a mutation: React runs this effect, unmounts and mounts again while
  // developing, and a mutation started before that remount never reports back to the remounted
  // component. The two callbacks are the shell's, so they hold whatever happens to this one.
  useEffect(() => {
    if (asked.current) return;
    asked.current = true;
    openSession(client, placeOf(ask), null).then((opened) => {
      onOpened(opened.runId);
      onClose();
    }, setFailure);
  }, [ask, client, onOpened, onClose]);

  if (failure === null) return null;

  return (
    <Dialog
      title={t("session.terminalTitle")}
      onClose={onClose}
      width={440}
      footer={
        <Button variant="outline" size="sm" onClick={onClose} style={{ marginLeft: "auto" }}>
          {t("dialog.close")}
        </Button>
      }
    >
      <p role="alert" style={{ margin: 0, color: "var(--destructive)" }}>
        {t("session.terminalFailed", { reason: explain(failure) })}
      </p>
    </Dialog>
  );
}

/**
 * The models a person can choose for an agent: its provider's list, with the folder's own model in
 * front when the folder names one the list does not have. Nothing is labelled the default; the
 * folder's agent and model are simply the ones selected.
 */
export function modelsFor(tool: MachineTool, folder: SessionAsk["folder"]): string[] {
  const own = tool.adapter === folder.defaultAgent ? folder.defaultModel : null;
  return own && !tool.models.includes(own) ? [own, ...tool.models] : [...tool.models];
}

function firstModel(tool: MachineTool | undefined, folder: SessionAsk["folder"]): string {
  if (!tool) return "";
  const own = tool.adapter === folder.defaultAgent ? folder.defaultModel : null;
  return own ?? modelsFor(tool, folder)[0] ?? "";
}

function Explore({
  ask,
  onOpened,
  onClose,
}: {
  ask: SessionAsk;
  onOpened: (runId: string) => void;
  onClose: () => void;
}) {
  const tools = useTools();
  const agents = tools.data?.agents ?? [];

  let body;
  if (tools.error)
    body = (
      <ErrorState
        detail={t("session.toolsFailed", { reason: explain(tools.error) })}
        onRetry={() => void tools.refetch()}
      />
    );
  else if (!tools.data) body = <Loading />;
  else if (!agents.some((tool) => tool.path !== null))
    body = <p style={{ margin: 0, color: "var(--muted-foreground)" }}>{t("session.noAgent")}</p>;

  if (body)
    return (
      <Dialog
        title={t("session.exploreTitle")}
        onClose={onClose}
        width={560}
        footer={
          <Button variant="outline" size="sm" onClick={onClose} style={{ marginLeft: "auto" }}>
            {t("session.cancel")}
          </Button>
        }
      >
        {body}
      </Dialog>
    );

  return <ExploreForm ask={ask} agents={agents} onOpened={onOpened} onClose={onClose} />;
}

function ExploreForm({
  ask,
  agents,
  onOpened,
  onClose,
}: {
  ask: SessionAsk;
  agents: MachineTool[];
  onOpened: (runId: string) => void;
  onClose: () => void;
}) {
  const { folder, pbi } = ask;
  const usable = agents.filter((tool) => tool.path !== null);
  const preferred = usable.find((tool) => tool.adapter === folder.defaultAgent) ?? usable[0];
  const [agent, setAgent] = useState(preferred?.adapter ?? "");
  const [model, setModel] = useState(() => firstModel(preferred, folder));
  const [command, setCommand] = useState("");
  const commandInput = useRef<HTMLInputElement>(null);
  const open = useOpenSession();
  useOpened(open.data, onOpened, onClose);

  const chosen = agents.find((tool) => tool.adapter === agent);
  const models = chosen ? modelsFor(chosen, folder) : [];

  const pick = (adapter: string) => {
    setAgent(adapter);
    setModel(
      firstModel(
        agents.find((tool) => tool.adapter === adapter),
        folder,
      ),
    );
  };

  const suggest = (text: string) => {
    setCommand(text);
    commandInput.current?.focus();
  };

  const submit = () =>
    open.mutate({
      place: placeOf(ask),
      agent: {
        provider: agent,
        model: model === "" ? null : model,
        command: command.trim() || null,
      },
    });

  const suggestions = pbi
    ? pbi.suggestion
      ? [{ label: pbi.suggestion, text: pbi.suggestion }]
      : []
    : [{ label: t("session.exploreIdea"), text: "/aio:explore " }];

  return (
    <Dialog
      title={t("session.exploreTitle")}
      onClose={onClose}
      width={560}
      footer={
        <>
          <span style={{ flex: 1 }} />
          <Button variant="outline" size="sm" onClick={onClose}>
            {t("session.cancel")}
          </Button>
          <Button size="sm" disabled={!chosen?.path || open.isPending} onClick={submit}>
            {open.isPending ? t("session.opening") : t("session.open")}
          </Button>
        </>
      }
    >
      <p
        style={{
          margin: "-10px 0 0",
          fontSize: "var(--text-xs)",
          color: "var(--muted-foreground)",
        }}
      >
        {pbi
          ? t("session.onPbi", { folder: folder.name, id: pbi.id })
          : t("session.onFolder", { folder: folder.name })}
        <span style={{ fontFamily: "var(--font-mono)" }}>
          {pbi ? `change/${pbi.id}` : folder.path}
        </span>
      </p>

      <form
        onSubmit={(event) => {
          event.preventDefault();
          if (chosen?.path && !open.isPending) submit();
        }}
        style={{ display: "flex", flexDirection: "column", gap: 16 }}
      >
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))",
            gap: 16,
          }}
        >
          <Field label={t("session.agent")} hint={t("session.agentHint")}>
            <Select
              mono
              autoFocus
              value={agent}
              onChange={(event) => pick(event.target.value)}
            >
              {agents.map((tool) => (
                <option key={tool.adapter} value={tool.adapter} disabled={tool.path === null}>
                  {tool.path === null
                    ? t("session.agentMissing", { agent: tool.adapter })
                    : tool.version
                      ? t("session.agentVersion", { agent: tool.adapter, version: tool.version })
                      : tool.adapter}
                </option>
              ))}
            </Select>
          </Field>
          <Field label={t("session.model")} hint={t("session.modelHint")}>
            <Select mono value={model} onChange={(event) => setModel(event.target.value)}>
              {models.length === 0 ? <option value="">{t("session.modelOwn")}</option> : null}
              {models.map((name) => (
                <option key={name} value={name}>
                  {name}
                </option>
              ))}
            </Select>
          </Field>
        </div>

        <Field label={t("session.command")} hint={t("session.commandHint")}>
          <input
            ref={commandInput}
            className="pc-input"
            spellCheck={false}
            value={command}
            onChange={(event) => setCommand(event.target.value)}
            style={{ height: 36, fontFamily: "var(--font-mono)" }}
          />
        </Field>

        {suggestions.length > 0 ? (
          <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 6 }}>
            <span style={{ fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>
              {t("session.suggested")}
            </span>
            {suggestions.map((suggestion) => (
              <Button
                key={suggestion.label}
                variant="outline"
                size="xs"
                onClick={() => suggest(suggestion.text)}
                style={{ fontFamily: "var(--font-mono)" }}
              >
                {suggestion.label}
              </Button>
            ))}
          </div>
        ) : null}
      </form>

      <p style={{ margin: 0, fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>
        {pbi ? t("session.notePbi") : t("session.noteFolder")}
      </p>
      {open.error ? (
        <p
          role="alert"
          style={{ margin: 0, color: "var(--destructive)", fontSize: "var(--text-xs)" }}
        >
          {explain(open.error)}
        </p>
      ) : null}
    </Dialog>
  );
}
