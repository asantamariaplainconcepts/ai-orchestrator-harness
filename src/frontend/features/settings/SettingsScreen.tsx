import { useState } from "react";
import { Link } from "react-router";
import { explain } from "@/shared/http/client";
import { type MachineTool, usePreferences, useSetEditor, useTools } from "@/shared/http/settings";
import { t } from "@/shared/i18n";
import { Badge, Button, ErrorState, Field, Loading } from "@/shared/ui/kit";
import { EngineSection } from "./EngineSection";
import { PermissionsSection } from "./PermissionsSection";
import { StartAtLoginSection } from "./StartAtLoginSection";
import { StopSection } from "./StopSection";
import { UpdateSection } from "./UpdateSection";

/**
 * Settings (#515, DESIGN.md#the-screens): what the console works with. The CLIs on this machine,
 * asked of the server's adapters; the editor of *Open in editor*; the engine's interval and stale
 * threshold (#562); what the agents may do without asking (#578); the updates and *Stop console*. A folder's setup, its loop and its defaults are
 * on its own page (#565), which its name in the rail opens. There is no screen for sources: the
 * credential is `gh`'s.
 */
export function SettingsScreen() {
  return (
    <div
      style={{
        padding: "24px clamp(16px, 4vw, 40px) 40px",
        display: "flex",
        flexDirection: "column",
        gap: 28,
      }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
        <h1 style={{ margin: 0, fontSize: "var(--text-lg)", fontWeight: 600 }}>
          {t("screen.settings")}
        </h1>
        <p style={note}>{t("settings.summary")}</p>
      </div>

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(min(100%, 420px), 1fr))",
          gap: 24,
          maxWidth: 1100,
        }}
      >
        <Machine />

        <div style={{ display: "flex", flexDirection: "column", gap: 24, minWidth: 0 }}>
          <Section title={t("settings.engine")} hint={t("settings.engineHint")}>
            <EngineSection />
          </Section>

          <Section title={t("settings.permissions")} hint={t("settings.permissionsHint")}>
            <PermissionsSection />
          </Section>
        </div>
      </div>
    </div>
  );
}

/** "On this machine": the vendor CLIs, the agent CLIs, `az`, and the editor. */
function Machine() {
  const tools = useTools();

  return (
    <Section title={t("settings.machine")} hint={t("settings.machineHint")}>
      {/* #680. The same probes with what no adapter drives, and how to install each. */}
      <Link to="/requirements" style={{ ...note, color: "var(--primary)", alignSelf: "flex-start" }}>
        {t("requirements.open")}
      </Link>
      {tools.isPending ? <Loading /> : null}
      {tools.error ? (
        <ErrorState
          detail={t("settings.machineFailed", { reason: explain(tools.error) })}
          onRetry={() => void tools.refetch()}
        />
      ) : null}
      {tools.data ? (
        <List>
          {tools.data.vendors.map((tool) => (
            <ToolRow key={`vendor:${tool.adapter}`} tool={tool} vendor />
          ))}
          {tools.data.agents.map((tool) => (
            <ToolRow key={`agent:${tool.adapter}`} tool={tool} vendor={false} />
          ))}
          <div style={toolRow(true)}>
            <span style={toolName}>az</span>
            <span style={{ color: "var(--muted-foreground)" }}>{t("settings.az")}</span>
            <span style={{ marginLeft: "auto", ...note }}>{t("settings.notYetUsed")}</span>
          </div>
        </List>
      ) : null}
      <Editor />
      <UpdateSection />
      <StartAtLoginSection />
      <StopSection />
    </Section>
  );
}

function ToolRow({ tool, vendor }: { tool: MachineTool; vendor: boolean }) {
  const version = tool.version ?? t("settings.noVersion");
  const missing = tool.path === null;
  const signedOut = !missing && (vendor ? tool.account === null : tool.problem !== null);
  const detail = missing
    ? vendor
      ? t("settings.vendorMissing")
      : t("settings.agentMissing")
    : signedOut
      ? t("settings.noAccount", { version, problem: tool.problem ?? "" })
      : vendor
        ? t("settings.loggedIn", { version, account: tool.account ?? "" })
        : t("settings.at", { version, path: tool.path ?? "" });

  return (
    <div style={toolRow(false)}>
      <span style={toolName} title={tool.adapter}>
        {vendor ? tool.cli : tool.adapter}
      </span>
      <span style={{ minWidth: 0, color: "var(--muted-foreground)", overflowWrap: "anywhere" }}>{detail}</span>
      <span style={{ marginLeft: "auto", flex: "none" }}>
        <Badge variant={missing || signedOut ? "warning" : "success"}>
          {missing ? t("settings.missing") : signedOut ? t("settings.signedOut") : t("settings.found")}
        </Badge>
      </span>
    </div>
  );
}

function Editor() {
  const preferences = usePreferences();
  const save = useSetEditor();
  const [editing, setEditing] = useState<string | null>(null);

  if (!preferences.data) return null;

  if (editing === null) {
    return (
      <p style={{ ...note, margin: "4px 0 0", display: "flex", alignItems: "center", gap: 6, flexWrap: "wrap" }}>
        <span>{t("settings.editor")}:</span>
        <span style={{ fontFamily: "var(--font-mono)", color: "var(--foreground)" }}>{preferences.data.editor}</span>
        <Button
          variant="ghost"
          size="xs"
          onClick={() => setEditing(preferences.data.editor)}
          style={{ color: "var(--primary)" }}
        >
          {t("settings.change")}
        </Button>
      </p>
    );
  }

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        save.mutate(editing.trim(), { onSuccess: () => setEditing(null) });
      }}
      onKeyDown={(event) => {
        if (event.key === "Escape") setEditing(null);
      }}
      style={{ display: "flex", alignItems: "flex-end", gap: 8, flexWrap: "wrap", marginTop: 4 }}
    >
      <div style={{ flex: "1 1 200px" }}>
        <Field label={t("settings.editorField")} hint={t("settings.editorHint")}>
          <input
            className="pc-input"
            autoFocus
            spellCheck={false}
            value={editing}
            onChange={(event) => setEditing(event.target.value)}
            style={{ height: 32, fontFamily: "var(--font-mono)" }}
          />
        </Field>
      </div>
      <Button size="sm" type="submit" disabled={editing.trim() === "" || save.isPending}>
        {t("settings.save")}
      </Button>
      <Button variant="outline" size="sm" onClick={() => setEditing(null)}>
        {t("settings.cancel")}
      </Button>
      {save.error ? <p role="alert" style={{ ...failure, flexBasis: "100%" }}>{explain(save.error)}</p> : null}
    </form>
  );
}

function Section({ title, hint, children }: { title: string; hint: string; children: React.ReactNode }) {
  return (
    <section style={{ display: "flex", flexDirection: "column", gap: 8, minWidth: 0 }}>
      <div style={{ display: "flex", alignItems: "baseline", justifyContent: "space-between", gap: 12, flexWrap: "wrap" }}>
        <h2 style={sectionTitle}>{title}</h2>
        <span style={note}>{hint}</span>
      </div>
      {children}
    </section>
  );
}

function List({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        border: "1px solid var(--border)",
        borderRadius: "var(--radius-xl)",
        overflow: "hidden",
      }}
    >
      {children}
    </div>
  );
}

const note: React.CSSProperties = {
  margin: 0,
  fontSize: "var(--text-xs)",
  color: "var(--muted-foreground)",
};

const failure: React.CSSProperties = {
  margin: 0,
  fontSize: "var(--text-xs)",
  color: "var(--destructive)",
};

const sectionTitle: React.CSSProperties = {
  margin: 0,
  fontSize: "var(--text-xs)",
  fontWeight: 600,
  color: "var(--muted-foreground)",
  textTransform: "uppercase",
  letterSpacing: "0.04em",
};

const toolName: React.CSSProperties = {
  flex: "none",
  width: 110,
  fontFamily: "var(--font-mono)",
};

function toolRow(last: boolean): React.CSSProperties {
  return {
    display: "flex",
    alignItems: "center",
    gap: 12,
    padding: "10px 14px",
    borderBottom: last ? 0 : "1px solid var(--border)",
  };
}
