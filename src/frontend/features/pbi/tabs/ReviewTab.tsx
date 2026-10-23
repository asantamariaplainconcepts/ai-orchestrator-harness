import { useState, type ReactNode } from "react";
import { scriptOutputUrl, type JournalLine } from "@/shared/http/api";
import { explain } from "@/shared/http/client";
import {
  attachmentUrl,
  useLoopDefinition,
  usePbiJournal,
  useReview,
  useReviewVerdict,
  type ReviewRead,
  type ReviewSubmitted,
} from "@/shared/http/queries";
import { t, tCount, type TranslationKey } from "@/shared/i18n";
import { Badge, Button, Dialog, ErrorState, Field, Loading } from "@/shared/ui/kit";
import { when } from "../format";
import { loopState } from "../journey";
import type { PbiTabProps } from "../PbiTabProps";
import { Diff, Refusal } from "./review/Diff";

/**
 * Review (DESIGN.md, Review; #513): what the change touched, and what was seen of
 * it. The pull request with its base and head and the two verdicts, the files, the diff of the one
 * chosen with its threads, the checks on the head, the quality of the change (#746) and the captures
 * of the evidence, in that order.
 *
 * Both verdicts write to the vendor as the person. Approve releases the hold, as an approval seen by
 * the engine would; Request changes copies the review to the issue and sends a held PBI to its
 * state's `back`, and is not offered in a state without one. What the loop did, or why it did not
 * move, is said under the buttons. A merged or closed pull request offers neither (#719): there is
 * nothing left to decide.
 */
export default function ReviewTab({ repository, id, detail }: PbiTabProps) {
  const review = useReview(repository, id);
  const loop = useLoopDefinition(repository);
  const state = detail.pbi.state;
  const back = loopState(loop.data, state)?.back ?? null;

  if (review.error)
    return (
      <ErrorState
        detail={t("review.failed", { reason: explain(review.error) })}
        onRetry={() => void review.refetch()}
      />
    );
  if (!review.data) return <Loading />;

  const { pullRequest, change } = review.data;
  if (!pullRequest || !change)
    return (
      <div className="pc-empty">
        <div className="pc-empty__header">
          <span className="pc-empty__desc">{t("review.noPullRequest", { id })}</span>
        </div>
      </div>
    );

  return <Review repository={repository} id={id} read={review.data} state={state} back={back} />;
}

function Review({
  repository,
  id,
  read,
  state,
  back,
}: {
  repository: string;
  id: string;
  read: ReviewRead;
  state: string | null;
  back: string | null;
}) {
  const pull = read.pullRequest!;
  const change = read.change!;
  const [chosen, setChosen] = useState<string | null>(null);
  const file = change.files.find((entry) => entry.path === chosen) ?? change.files[0];
  const short = (sha: string) => sha.slice(0, 7);
  const decided = pull.isMerged || pull.isClosed;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 24, maxWidth: 1100, minWidth: 0 }}>
      <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12, flexWrap: "wrap" }}>
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 12,
              flexWrap: "wrap",
              fontSize: "var(--text-xs)",
              color: "var(--muted-foreground)",
              minWidth: 0,
            }}
          >
            <Mono>{t("review.base", { ref: change.baseRef, sha: short(change.baseSha) })}</Mono>
            <Mono>{t("review.head", { ref: change.headRef, sha: short(change.headSha) })}</Mono>
            <span>{tCount(change.commits, t("review.commits.one"), t("review.commits.other"))}</span>
            <Mono>
              <span style={{ color: "var(--pc-success-700)" }}>+{change.additions}</span>{" "}
              <span style={{ color: "var(--pc-error-700)" }}>−{change.deletions}</span>
            </Mono>
            <Badge variant={pull.isMerged ? "secondary" : pull.isApproved ? "success" : "outline"}>
              {t(
                pull.isMerged
                  ? "review.state.merged"
                  : pull.isClosed
                    ? "review.state.closed"
                    : pull.isApproved
                      ? "review.state.approved"
                      : pull.isDraft
                        ? "review.state.draft"
                        : "review.state.open",
              )}
            </Badge>
          </div>
          {decided ? null : <Verdicts repository={repository} id={id} pull={pull.id} back={back} />}
        </div>
        <p style={{ margin: 0, fontSize: "var(--text-xs)", color: "var(--muted-foreground)", maxWidth: 760 }}>
          {pull.isMerged
            ? t("review.explainMerged", { pr: pull.id })
            : pull.isClosed
              ? t("review.explainClosed", { pr: pull.id })
              : back
                ? t("review.explain", { pr: pull.id, id, back })
                : state
                  ? t("review.explainNoBack", { pr: pull.id, state })
                  : t("review.explainOutside", { pr: pull.id })}
        </p>
      </div>

      <Section
        title={
          change.files.length < change.changedFiles
            ? t("review.filesCut", { shown: String(change.files.length), count: String(change.changedFiles) })
            : t("review.files", { count: String(change.files.length) })
        }
      >
        {change.files.length === 0 ? (
          <Quiet>{t("review.noFiles")}</Quiet>
        ) : (
          <ul
            aria-label={t("review.filesLabel")}
            style={{
              listStyle: "none",
              margin: 0,
              padding: 0,
              border: "1px solid var(--border)",
              borderRadius: "var(--radius-lg)",
              overflow: "hidden",
            }}
          >
            {change.files.map((entry, index) => {
              const selected = entry.path === file?.path;
              const threads = read.threads.filter((thread) => thread.path === entry.path && !thread.isResolved).length;
              return (
                <li key={entry.path} style={{ borderTop: index === 0 ? undefined : "1px solid var(--border)" }}>
                  <button
                    type="button"
                    aria-pressed={selected}
                    onClick={() => setChosen(entry.path)}
                    className="row-hover"
                    style={{
                      width: "100%",
                      display: "flex",
                      alignItems: "center",
                      gap: 10,
                      padding: "8px 12px",
                      border: 0,
                      background: selected ? "var(--accent)" : "transparent",
                      color: selected ? "var(--accent-foreground)" : "var(--foreground)",
                      textAlign: "left",
                      cursor: "pointer",
                      font: "inherit",
                    }}
                  >
                    <Mono style={{ color: "var(--pc-success-700)", width: 48, flex: "none" }}>+{entry.additions}</Mono>
                    <Mono style={{ color: "var(--pc-error-700)", width: 40, flex: "none" }}>−{entry.deletions}</Mono>
                    <Mono style={{ flex: 1, minWidth: 0, overflowWrap: "anywhere" }}>{entry.path}</Mono>
                    {threads > 0 ? <Badge variant="info">{threads}</Badge> : null}
                    <Badge variant="outline">{entry.status}</Badge>
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </Section>

      {file ? (
        <Section title={t("review.diff", { path: file.path })}>
          <Diff
            key={file.path}
            repository={repository}
            id={id}
            file={file}
            threads={read.threads.filter((thread) => thread.path === file.path)}
          />
        </Section>
      ) : null}

      <Section title={t("review.gate", { sha: short(change.headSha) })}>
        {read.checks.problem ? (
          <Quiet tone="error">{t("review.checksFailed", { reason: read.checks.problem })}</Quiet>
        ) : read.checks.rows.length === 0 ? (
          <LocalGate repository={repository} id={id} sha={short(change.headSha)} />
        ) : (
          <ul aria-label={t("review.gate", { sha: short(change.headSha) })} style={rows}>
            {read.checks.rows.map((check, index) => (
              <li
                key={`${check.name}-${index}`}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 12,
                  padding: "8px 12px",
                  borderTop: index === 0 ? undefined : "1px solid var(--border)",
                }}
              >
                <Badge variant={checkBadge[check.state] ?? "outline"}>{t(checkWord[check.state] ?? "review.check.neutral")}</Badge>
                {check.url ? (
                  <a href={check.url} target="_blank" rel="noreferrer" style={{ flex: 1, minWidth: 0, overflowWrap: "anywhere" }}>
                    {check.name}
                  </a>
                ) : (
                  <span style={{ flex: 1, minWidth: 0, overflowWrap: "anywhere" }}>{check.name}</span>
                )}
              </li>
            ))}
          </ul>
        )}
      </Section>

      <Quality id={id} read={read} headSha={change.headSha} />

      <Evidence repository={repository} id={id} read={read} headSha={change.headSha} />
    </div>
  );
}

/**
 * The gate as this machine ran it, for a vendor that reports no checks (#719): the newest `ran` line of
 * the PBI's `gate` in the journal, its why and its log. The journal is this machine's, so a gate run
 * by another instance leaves no line here, and the sentence says where to look instead.
 */
function LocalGate({ repository, id, sha }: { repository: string; id: string; sha: string }) {
  const journal = usePbiJournal(repository, id);
  const ran = (journal.data ?? []).find((line: JournalLine) => line.kind === "ran" && line.why.startsWith(gatePrefix));
  if (!ran) return <Quiet>{t("review.noChecks", { sha })}</Quiet>;

  const log = scriptOutputUrl(ran);
  return (
    <Quiet>
      {t("review.gateRan", { at: when(ran.at), why: ran.why.slice(gatePrefix.length) })}
      {log ? (
        <>
          {" "}
          <a href={log} target="_blank" rel="noreferrer">
            {t("review.gateLog")}
          </a>
        </>
      ) : null}
    </Quiet>
  );
}

/** How the engine names the gate on its `ran` line: `<script>: <the script's why>`. */
const gatePrefix = "gate: ";

/** Approve, and Request changes behind a dialog that asks what has to change. */
function Verdicts({ repository, id, pull, back }: { repository: string; id: string; pull: string; back: string | null }) {
  const verdict = useReviewVerdict(repository, id);
  const [asking, setAsking] = useState(false);
  const [body, setBody] = useState("");
  const [done, setDone] = useState<{ kind: "approve" | "request-changes"; answer: ReviewSubmitted } | null>(null);

  const approve = () => {
    verdict.reset();
    setDone(null);
    verdict.mutate({ kind: "approve" }, { onSuccess: (answer) => setDone({ kind: "approve", answer }) });
  };

  const request = () =>
    verdict.mutate(
      { kind: "request-changes", body },
      {
        onSuccess: (answer) => {
          setDone({ kind: "request-changes", answer });
          setAsking(false);
          setBody("");
        },
      },
    );

  return (
    <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 6, maxWidth: "100%" }}>
      <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
        <Button size="sm" onClick={approve} disabled={verdict.isPending}>
          {t("review.approve")}
        </Button>
        {back ? (
          <Button
            size="sm"
            variant="outline"
            disabled={verdict.isPending}
            onClick={() => {
              verdict.reset();
              setDone(null);
              setAsking(true);
            }}
          >
            {t("review.requestChanges")}
          </Button>
        ) : null}
      </div>

      {done ? (
        <span role="status" style={{ fontSize: "var(--text-xs)", color: "var(--muted-foreground)", textAlign: "right" }}>
          {done.answer.asComment
            ? t(done.kind === "approve" ? "review.approvedAsComment" : "review.changesRequestedAsComment", {
                pr: pull,
                reason: done.answer.asComment,
              })
            : t(done.kind === "approve" ? "review.approved" : "review.changesRequested", { pr: pull })}{" "}
          {done.answer.line
            ? t("review.moved", { id, from: done.answer.line.from, to: done.answer.line.to ?? "" })
            : t("review.unmoved", { reason: done.answer.unmoved ?? "" })}
          {done.answer.note ? ` ${done.answer.note}` : ""}
        </span>
      ) : null}
      {verdict.error && !asking ? <Refusal reason={explain(verdict.error)} /> : null}

      {asking ? (
        <Dialog
          title={t("review.requestChanges")}
          onClose={() => setAsking(false)}
          width={480}
          footer={
            <>
              <Button variant="outline" size="sm" onClick={() => setAsking(false)} style={{ marginLeft: "auto" }}>
                {t("review.cancel")}
              </Button>
              <Button size="sm" onClick={request} disabled={verdict.isPending || body.trim() === ""}>
                {t("review.requestChanges")}
              </Button>
            </>
          }
        >
          <Field label={t("review.requestBody")} hint={t("review.requestHint", { pr: pull, id })}>
            <textarea
              className="pc-textarea"
              rows={5}
              autoFocus
              value={body}
              onChange={(event) => setBody(event.target.value)}
            />
          </Field>
          {verdict.error ? <Refusal reason={explain(verdict.error)} /> : null}
        </Dialog>
      ) : null}
    </div>
  );
}

/**
 * What `.aio/steps/quality` measured, from the newest comment it left on the issue (#746): one row
 * per check with a dot for its result, the measure or why it was not measured, and its line. The
 * server says when it measured another commit than the head.
 */
function Quality({ id, read, headSha }: { id: string; read: ReviewRead; headSha: string }) {
  const quality = read.quality;
  const short = (sha: string) => sha.slice(0, 7);

  if (quality.rows.length === 0)
    return (
      <Section title={t("review.qualityTitle")}>
        {quality.problem ? (
          <Quiet tone="error">{t("review.qualityFailed", { reason: quality.problem })}</Quiet>
        ) : (
          <Quiet>{t("review.noQuality", { id })}</Quiet>
        )}
      </Section>
    );

  const measured = short(quality.headSha ?? "");
  return (
    <Section title={t("review.quality", { sha: measured })}>
      {quality.stale ? <Quiet tone="warning">{t("review.qualityStale", { sha: measured, head: short(headSha) })}</Quiet> : null}
      <ul aria-label={t("review.qualityLabel")} style={rows}>
        {quality.rows.map((row, index) => {
          const result = resultWord[row.result] ?? "review.result.notMeasured";
          return (
            <li
              key={`${row.check}-${index}`}
              style={{
                display: "flex",
                alignItems: "baseline",
                flexWrap: "wrap",
                columnGap: 12,
                rowGap: 2,
                padding: "8px 12px",
                borderTop: index === 0 ? undefined : "1px solid var(--border)",
              }}
            >
              <span
                aria-hidden
                style={{
                  width: 8,
                  height: 8,
                  flex: "none",
                  alignSelf: "flex-start",
                  marginTop: 6,
                  borderRadius: "var(--radius-full)",
                  background: resultDot[row.result] ?? "var(--muted-foreground)",
                }}
              />
              <span style={{ fontWeight: 500, minWidth: 0 }}>{row.check}</span>
              <span
                style={{
                  flex: "1 1 240px",
                  minWidth: 0,
                  overflowWrap: "anywhere",
                  color: row.result === "not measured" ? "var(--muted-foreground)" : undefined,
                }}
              >
                {row.result === "not measured" ? t("review.qualityNotMeasured", { reason: row.measured }) : row.measured}
              </span>
              <span style={{ marginLeft: "auto", fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>
                {row.line ? `${t("review.qualityLine", { line: row.line })} · ` : ""}
                {t(result)}
              </span>
            </li>
          );
        })}
      </ul>
    </Section>
  );
}

/** The captures of the newest evidence the issue carries, each image read through the server. */
function Evidence({
  repository,
  id,
  read,
  headSha,
}: {
  repository: string;
  id: string;
  read: ReviewRead;
  headSha: string;
}) {
  const evidence = read.evidence;
  const short = (sha: string) => sha.slice(0, 7);

  if (evidence.captures.length === 0)
    return (
      <Section title={t("review.evidenceTitle")}>
        {evidence.problem ? (
          <Quiet tone="error">{t("review.evidenceFailed", { reason: evidence.problem })}</Quiet>
        ) : (
          <Quiet>{t("review.noEvidence", { id })}</Quiet>
        )}
      </Section>
    );

  const taken = evidence.headSha ?? "";
  return (
    <Section
      title={t("review.evidence", {
        count: tCount(evidence.captures.length, t("review.captures.one"), t("review.captures.other")),
        id,
        sha: short(taken),
      })}
    >
      {taken !== headSha ? (
        <Quiet tone="warning">{t("review.evidenceStale", { sha: short(taken), head: short(headSha) })}</Quiet>
      ) : null}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))", gap: 12 }}>
        {evidence.captures.map((capture) => {
          const source = attachmentUrl(repository, id, capture.url);
          const caption = t("review.caption", {
            screen: capture.screen,
            width: String(capture.width),
            height: String(capture.height),
            theme: capture.theme,
          });
          return (
            <figure
              key={capture.name}
              style={{
                margin: 0,
                display: "flex",
                flexDirection: "column",
                border: "1px solid var(--border)",
                borderRadius: "var(--radius-lg)",
                overflow: "hidden",
                background: "var(--card)",
              }}
            >
              <a href={source} target="_blank" rel="noreferrer" aria-label={t("review.openCapture", { name: capture.name })}>
                <img
                  src={source}
                  alt={caption}
                  loading="lazy"
                  style={{ display: "block", width: "100%", aspectRatio: `${capture.width} / ${capture.height}`, objectFit: "cover", background: "var(--muted)" }}
                />
              </a>
              <figcaption style={{ padding: "8px 10px", fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>
                {caption}
              </figcaption>
            </figure>
          );
        })}
      </div>
    </Section>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section aria-label={title} style={{ display: "flex", flexDirection: "column", gap: 10, minWidth: 0 }}>
      <h2 style={{ margin: 0, fontSize: "var(--text-sm)", fontWeight: 600, overflowWrap: "anywhere" }}>{title}</h2>
      {children}
    </section>
  );
}

function Quiet({ children, tone }: { children: ReactNode; tone?: "error" | "warning" }) {
  return (
    <p
      style={{
        margin: 0,
        fontSize: "var(--text-sm)",
        color: tone === "error" ? "var(--pc-error-700)" : tone === "warning" ? "var(--pc-warning-700)" : "var(--muted-foreground)",
      }}
    >
      {children}
    </p>
  );
}

function Mono({ children, style }: { children: ReactNode; style?: React.CSSProperties }) {
  return <span style={{ fontFamily: "var(--font-mono)", fontSize: 12, ...style }}>{children}</span>;
}

const rows: React.CSSProperties = {
  listStyle: "none",
  margin: 0,
  padding: 0,
  border: "1px solid var(--border)",
  borderRadius: "var(--radius-lg)",
  overflow: "hidden",
};

const checkWord: Record<string, TranslationKey> = {
  passing: "review.check.passing",
  failing: "review.check.failing",
  pending: "review.check.pending",
  neutral: "review.check.neutral",
};

const resultWord: Record<string, TranslationKey> = {
  ok: "review.result.ok",
  over: "review.result.over",
  "not measured": "review.result.notMeasured",
};

/** Green for ok, red for over, grey for not measured or anything else. */
const resultDot: Record<string, string> = {
  ok: "var(--success)",
  over: "var(--destructive)",
};

const checkBadge: Record<string, "success" | "error" | "info" | "outline"> = {
  passing: "success",
  failing: "error",
  pending: "info",
  neutral: "outline",
};
