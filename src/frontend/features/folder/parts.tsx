import type { CSSProperties, ReactNode } from "react";
import { explain } from "@/shared/http/client";

/** The small print of the folder page, one size for every line that qualifies a value. */
const small: CSSProperties = { fontSize: "var(--text-xs)" };

export const note: CSSProperties = { ...small, margin: 0, color: "var(--muted-foreground)" };
export const warning: CSSProperties = { ...small, color: "var(--pc-warning-700)" };
export const failure: CSSProperties = { ...small, margin: 0, color: "var(--destructive)" };

export const sectionTitle: CSSProperties = {
  ...note,
  fontWeight: 600,
  textTransform: "uppercase",
  letterSpacing: "0.04em",
};

/** A bordered list of `ListRow`s. */
export const list: CSSProperties = {
  ...small,
  display: "flex",
  flexDirection: "column",
  border: "1px solid var(--border)",
  borderRadius: "var(--radius-xl)",
  overflow: "hidden",
};

/** A command a person runs in their own terminal, shown whole however narrow the page. */
const command: CSSProperties = {
  ...small,
  display: "block",
  fontFamily: "var(--font-mono)",
  overflowWrap: "anywhere",
  padding: "4px 8px",
  border: "1px solid var(--border)",
  borderRadius: "var(--radius-md)",
  background: "var(--muted)",
};

/** A value that takes the room its label leaves and breaks a path rather than widen the page. */
const value: CSSProperties = { minWidth: 0, flex: "1 1 160px", overflowWrap: "anywhere" };

/** Why a call failed, as an alert; nothing while it has not. */
export function Failure({ error }: { error: Error | null | undefined }) {
  if (!error) return null;
  return (
    <p role="alert" style={failure}>
      {explain(error)}
    </p>
  );
}

/**
 * A label and its value: the label over the value, as the strip's cells have it, or beside it in a
 * fixed column when `inline`, as the checkout's facts have it.
 */
export function Labelled({
  label,
  inline = false,
  mono = false,
  children,
}: {
  label: string;
  inline?: boolean;
  mono?: boolean;
  children: ReactNode;
}) {
  if (!inline) {
    return (
      <div style={{ display: "flex", flexDirection: "column", gap: 6, minWidth: 0 }}>
        <span style={sectionTitle}>{label}</span>
        {children}
      </div>
    );
  }
  return (
    <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
      <span style={{ color: "var(--muted-foreground)", flex: "none", width: 110 }}>{label}</span>
      <span style={{ ...value, fontFamily: mono ? "var(--font-mono)" : undefined }}>
        {children}
      </span>
    </div>
  );
}

/**
 * Something the person has to put right: the finding in the tone it deserves, the sentence under
 * it, and the command when one does it. The console never runs the command itself.
 */
export function Remedy({
  tone,
  role,
  headline,
  detail,
  remedy,
}: {
  tone: "warning" | "failure";
  role?: string;
  headline: string;
  detail: string;
  remedy: string | null;
}) {
  return (
    <div role={role} style={{ display: "flex", flexDirection: "column", gap: 4, minWidth: 0 }}>
      <span style={tone === "warning" ? warning : failure}>{headline}</span>
      <span style={note}>{detail}</span>
      {remedy ? <code style={command}>{remedy}</code> : null}
    </div>
  );
}

/**
 * One row of a `list`: its name in the mono font, then whatever the row says. The environments'
 * rows are `dense`: they hold a 30px select, so they sit tighter and the name takes the room the
 * select leaves.
 */
export function ListRow({
  label,
  last,
  dense = false,
  children,
}: {
  label: string;
  last: boolean;
  dense?: boolean;
  children: ReactNode;
}) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 12,
        flexWrap: "wrap",
        padding: dense ? "7px 14px" : "9px 14px",
        borderBottom: last ? 0 : "1px solid var(--border)",
      }}
    >
      <span
        style={{
          ...value,
          fontFamily: "var(--font-mono)",
          flex: dense ? "1 1 160px" : "0 1 220px",
        }}
      >
        {label}
      </span>
      {children}
    </div>
  );
}
