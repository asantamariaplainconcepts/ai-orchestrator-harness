import { Fragment, useMemo, useState, type CSSProperties } from "react";
import { explain } from "@/shared/http/client";
import {
  useReviewThread,
  type ReviewFile,
  type ReviewThreadRow,
} from "@/shared/http/queries";
import { t } from "@/shared/i18n";
import { ago } from "@/shared/ui/ago";
import { Badge, Button } from "@/shared/ui/kit";
import { hunksOf } from "./hunks";

/**
 * One file of the pull request with its threads under the lines they are anchored to (#513).
 *
 * **The old diff view's approach, transplanted.** Hunks with an old and a new gutter, tinted rows,
 * and a gutter number that is a press — the shape of `PatchView` and the remote review's line pick
 * (#419). Here the new-line number opens a thread on that line, because the vendor anchors a thread
 * to the file's new version and the coder reads it there.
 *
 * A thread whose line is not in this diff any more is listed under it, not dropped: the vendor still
 * has it, and so does the coder's next run.
 */
export function Diff({
  repository,
  id,
  file,
  threads,
}: {
  repository: string;
  id: string;
  file: ReviewFile;
  threads: ReviewThreadRow[];
}) {
  const hunks = useMemo(() => hunksOf(file.patch), [file.patch]);
  const [composing, setComposing] = useState<number | null>(null);

  const onLines = new Set(hunks.flatMap((hunk) => hunk.lines.map((line) => line.newLine)));
  const anchored = (line: number) => threads.filter((thread) => thread.line === line);
  const elsewhere = threads.filter((thread) => thread.line === null || !onLines.has(thread.line));

  return (
    <div
      style={{
        border: "1px solid var(--border)",
        borderRadius: "var(--radius-lg)",
        background: "var(--card)",
        minWidth: 0,
        overflow: "hidden",
      }}
    >
      {file.previousPath ? (
        <div style={{ ...bar, fontFamily: "var(--font-mono)" }}>
          {t("review.renamedFrom", { path: file.previousPath })}
        </div>
      ) : null}

      {hunks.length === 0 ? (
        <div style={{ padding: 16, color: "var(--muted-foreground)" }}>{t("review.noPatch")}</div>
      ) : (
        <div style={{ overflowX: "auto" }}>
          {hunks.map((hunk) => (
            <div key={hunk.header} style={{ borderBottom: "1px solid var(--border)", minWidth: "fit-content" }}>
              <div style={{ ...bar, borderBottom: 0, fontFamily: "var(--font-mono)" }}>{hunk.header}</div>
              {hunk.lines.map((line, index) => (
                <Fragment key={index}>
                  <div style={{ display: "flex", ...row, background: tint[line.kind] }}>
                    <span style={gutter}>{line.oldLine ?? ""}</span>
                    {line.newLine === null ? (
                      <span style={gutter} />
                    ) : (
                      <button
                        type="button"
                        className="row-hover"
                        aria-label={t("review.commentOn", { line: String(line.newLine) })}
                        title={t("review.commentOn", { line: String(line.newLine) })}
                        aria-expanded={composing === line.newLine}
                        onClick={() => setComposing(composing === line.newLine ? null : line.newLine)}
                        style={{ ...gutter, border: 0, background: "transparent", font: "inherit", cursor: "pointer" }}
                      >
                        {line.newLine}
                      </button>
                    )}
                    <span style={{ width: 14, flex: "none", textAlign: "center", color: "var(--muted-foreground)" }}>
                      {line.kind === "added" ? "+" : line.kind === "removed" ? "−" : ""}
                    </span>
                    <span style={{ whiteSpace: "pre", paddingRight: 12 }}>{line.text}</span>
                  </div>
                  {line.newLine !== null
                    ? anchored(line.newLine).map((thread) => (
                        <Thread key={thread.id} repository={repository} id={id} thread={thread} />
                      ))
                    : null}
                  {line.newLine !== null && composing === line.newLine ? (
                    <Composer
                      repository={repository}
                      id={id}
                      path={file.path}
                      line={line.newLine}
                      onDone={() => setComposing(null)}
                    />
                  ) : null}
                </Fragment>
              ))}
            </div>
          ))}
        </div>
      )}

      {elsewhere.length > 0 ? (
        <section aria-label={t("review.elsewhere")} style={{ display: "flex", flexDirection: "column" }}>
          <div style={bar}>
            <strong style={{ fontWeight: 600, color: "var(--foreground)" }}>{t("review.elsewhere")}</strong>
            <span>· {t("review.elsewhereHint")}</span>
          </div>
          {elsewhere.map((thread) => (
            <Thread key={thread.id} repository={repository} id={id} thread={thread} />
          ))}
        </section>
      ) : null}
    </div>
  );
}

/** A thread under its line: its comments in order, a reply, and Resolve. */
function Thread({ repository, id, thread }: { repository: string; id: string; thread: ReviewThreadRow }) {
  const write = useReviewThread(repository, id);
  const [reply, setReply] = useState("");
  const first = thread.comments[0];
  const line = String(thread.line ?? "?");

  return (
    <section
      aria-label={t("review.thread", { line })}
      style={{
        margin: "6px 12px 10px 58px",
        padding: 10,
        display: "flex",
        flexDirection: "column",
        gap: 8,
        border: "1px solid var(--border)",
        borderRadius: "var(--radius-md)",
        background: "var(--background)",
        fontFamily: "var(--font-sans)",
        fontSize: "var(--text-sm)",
        color: thread.isResolved ? "var(--muted-foreground)" : "var(--foreground)",
        maxWidth: 720,
      }}
    >
      {thread.isResolved || thread.isOutdated ? (
        <div style={{ display: "flex", gap: 6 }}>
          {thread.isResolved ? <Badge variant="success">{t("review.resolved")}</Badge> : null}
          {thread.isOutdated ? <Badge variant="outline">{t("review.outdated")}</Badge> : null}
        </div>
      ) : null}

      {thread.comments.map((comment) => (
        <div key={comment.id} style={{ display: "flex", flexDirection: "column", gap: 2 }}>
          <span>
            <strong style={{ fontWeight: 600 }}>{comment.author ?? t("review.someone")}</strong>{" "}
            <span style={{ fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>· {ago(comment.at)}</span>
          </span>
          <span style={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere" }}>{comment.body}</span>
        </div>
      ))}

      {!thread.isResolved && first ? (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            if (reply.trim() === "") return;
            write.mutate({ kind: "reply", commentId: first.id, body: reply }, { onSuccess: () => setReply("") });
          }}
          style={{ display: "flex", gap: 6, alignItems: "center", flexWrap: "wrap" }}
        >
          <input
            className="pc-input"
            aria-label={t("review.replyLabel", { line })}
            placeholder={t("review.replyPlaceholder")}
            value={reply}
            onChange={(event) => setReply(event.target.value)}
            style={{ flex: "1 1 200px", minWidth: 0 }}
          />
          <Button type="submit" size="sm" variant="outline" disabled={write.isPending || reply.trim() === ""}>
            {t("review.reply")}
          </Button>
          <Button
            size="sm"
            variant="ghost"
            disabled={write.isPending}
            onClick={() => write.mutate({ kind: "resolve", threadId: thread.id })}
          >
            {t("review.resolve")}
          </Button>
        </form>
      ) : null}

      {write.error ? <Refusal reason={explain(write.error)} /> : null}
    </section>
  );
}

/** A new thread on one line, written on the pull request as the person. */
function Composer({
  repository,
  id,
  path,
  line,
  onDone,
}: {
  repository: string;
  id: string;
  path: string;
  line: number;
  onDone: () => void;
}) {
  const write = useReviewThread(repository, id);
  const [body, setBody] = useState("");

  return (
    <form
      aria-label={t("review.newThread", { path, line: String(line) })}
      onSubmit={(event) => {
        event.preventDefault();
        if (body.trim() === "") return;
        write.mutate({ kind: "comment", path, line, body }, { onSuccess: onDone });
      }}
      style={{
        margin: "6px 12px 10px 58px",
        padding: 10,
        display: "flex",
        flexDirection: "column",
        gap: 8,
        border: "1px solid var(--border)",
        borderRadius: "var(--radius-md)",
        background: "var(--muted)",
        fontFamily: "var(--font-sans)",
        fontSize: "var(--text-sm)",
        maxWidth: 720,
      }}
    >
      <span style={{ fontSize: "var(--text-xs)", fontWeight: 600, overflowWrap: "anywhere" }}>
        {t("review.newThread", { path, line: String(line) })}
      </span>
      <textarea
        className="pc-textarea"
        aria-label={t("review.threadBody")}
        placeholder={t("review.threadPlaceholder", { line: String(line) })}
        rows={3}
        autoFocus
        value={body}
        onChange={(event) => setBody(event.target.value)}
      />
      <div style={{ display: "flex", gap: 8 }}>
        <Button type="submit" size="sm" disabled={write.isPending || body.trim() === ""}>
          {t("review.comment")}
        </Button>
        <Button size="sm" variant="ghost" onClick={onDone}>
          {t("review.cancel")}
        </Button>
      </div>
      {write.error ? <Refusal reason={explain(write.error)} /> : null}
    </form>
  );
}

export function Refusal({ reason }: { reason: string }) {
  return (
    <span role="alert" style={{ fontSize: "var(--text-xs)", color: "var(--pc-error-700)" }}>
      {t("review.refused", { reason })}
    </span>
  );
}

const bar: CSSProperties = {
  display: "flex",
  alignItems: "center",
  gap: 6,
  flexWrap: "wrap",
  padding: "4px 12px",
  background: "var(--muted)",
  borderBottom: "1px solid var(--border)",
  fontSize: 11,
  color: "var(--muted-foreground)",
};

const row: CSSProperties = {
  fontFamily: "var(--font-mono)",
  fontSize: 11.5,
  lineHeight: 1.55,
};

// A tint of the semantic colour, not the colour itself: a solid green row is unreadable and a
// diff is mostly context. Mixed from the 500 base rather than the old view's 100 tint, which stays
// pale in the dark theme and leaves the row's text unreadable on it.
const tint: Record<"added" | "removed" | "context", string> = {
  added: "color-mix(in srgb, var(--success) 14%, transparent)",
  removed: "color-mix(in srgb, var(--destructive) 14%, transparent)",
  context: "transparent",
};

const gutter: CSSProperties = {
  width: 44,
  flex: "none",
  textAlign: "right",
  paddingRight: 8,
  color: "var(--muted-foreground)",
  userSelect: "none",
};
