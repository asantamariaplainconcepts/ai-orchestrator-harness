import { MessageSquarePlus } from "lucide-react";
import { useState, type ReactNode } from "react";
import { explain } from "@/shared/http/client";
import {
  attachmentUrl,
  proposalFileUrl,
  usePbiAction,
  useProposal,
  useProposalComment,
  useLoopDefinition,
  useRequestProposalChanges,
  type ProposalComment,
  type ProposalFile,
  type ProposalPage,
  type ProposalPlan,
} from "@/shared/http/queries";
import { t } from "@/shared/i18n";
import { Badge, Button, Dialog, ErrorState, Field, Loading } from "@/shared/ui/kit";
import { useNarrowContainer } from "@/shared/ui/useNarrowContainer";
import { when } from "../format";
import { ImageViewer } from "../issue/ImageViewer";
import { loopState } from "../journey";
import { VendorMarkdown, type Picture } from "../issue/VendorMarkdown";
import type { PbiTabProps } from "../PbiTabProps";
import { blocks, resolve, type Block } from "../proposal/blocks";

/**
 * The Proposal tab (DESIGN.md#the-screens, #512): what the change says it will do. The OpenSpec
 * change's files on the PBI's branch, read at its pull request's head, rendered with their line
 * numbers and their images: proposal, design, the spec deltas — each with what it adds, modifies
 * and removes, and the capability's spec on master beside it — and tasks with their count.
 *
 * Hovering a line offers to anchor a comment to it. A thread is the pull request's review comments
 * on that file and that line, so the coder reads it in its next run. While the PBI's state names
 * this tab with `tab: proposal`, Approve releases the hold, and Request changes, when the state
 * declares `back`, posts the review and sends the PBI there.
 *
 * A repository that does not plan in OpenSpec has its plan in the issue (#726): with no change to
 * show, the tab shows the issue's latest `## Plan` comment, links the earlier ones, and decides the
 * same way.
 */
export default function ProposalTab({ repository, id, detail }: PbiTabProps) {
  const proposal = useProposal(repository, id);
  const loop = useLoopDefinition(repository);
  const [measure, narrow] = useNarrowContainer<HTMLDivElement>(760);
  const [chosen, setChosen] = useState<string | null>(null);
  const [enlarged, setEnlarged] = useState<Picture | null>(null);

  if (proposal.error)
    return (
      <ErrorState
        detail={t("proposal.failed", { reason: explain(proposal.error) })}
        onRetry={() => void proposal.refetch()}
      />
    );
  if (!proposal.data) return <Loading />;

  const page = proposal.data;
  const state = loopState(loop.data, detail.pbi.state);
  const reviewing = state?.tab === "proposal";
  const decide = reviewing ? (
    <Decide repository={repository} id={id} held={detail.pbi.held} back={state?.back ?? null} page={page} />
  ) : null;
  const hint = reviewing ? (
    <p style={{ margin: "-8px 0 0", fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>
      {state?.back
        ? t(page.plan ? "proposal.decideHintPlan" : "proposal.decideHint", { pr: page.pullRequestId ?? "", back: state.back })
        : t("proposal.decideHintNoBack", { state: detail.pbi.state ?? "" })}
    </p>
  ) : null;

  if (page.plan)
    return (
      <PlanView repository={repository} id={id} page={page} plan={page.plan} decide={decide} hint={hint} onEnlarge={setEnlarged}>
        {enlarged ? <ImageViewer picture={enlarged} onClose={() => setEnlarged(null)} /> : null}
      </PlanView>
    );
  if (page.missing !== null || page.files.length === 0)
    return (
      <div className="pc-empty" style={{ maxWidth: 1100 }}>
        <div className="pc-empty__header">
          <span className="pc-empty__desc">{page.missing ?? t("proposal.noFiles")}</span>
        </div>
      </div>
    );

  const file = page.files.find((entry) => entry.path === chosen) ?? page.files[0];
  if (!file) return null;
  const tasks = page.files.find((entry) => entry.tasks !== null)?.tasks ?? null;

  return (
    <div ref={measure} style={{ display: "flex", flexDirection: "column", gap: 16, maxWidth: 1100, minWidth: 0 }}>
      <Bar decide={decide}>
        <span style={{ fontFamily: "var(--font-mono)", overflowWrap: "anywhere" }}>{page.root}</span>
        <span>
          {t("proposal.at", { head: (page.head ?? "").slice(0, 7), pr: page.pullRequestId ?? "" })}
        </span>
        {tasks ? (
          <span>{t("proposal.tasks", { done: String(tasks.done), total: String(tasks.total) })}</span>
        ) : null}
      </Bar>
      {hint}

      <div
        style={{
          display: "grid",
          gridTemplateColumns: narrow ? "minmax(0, 1fr)" : "240px minmax(0, 1fr)",
          gap: narrow ? 12 : 24,
          alignItems: "start",
        }}
      >
        <nav aria-label={t("proposal.files")} style={{ display: "flex", flexDirection: "column", gap: 2, minWidth: 0 }}>
          {page.files.map((entry) => (
            <FileLink
              key={entry.path}
              file={entry}
              threads={threads(page.comments.filter((comment) => comment.path === entry.path)).length}
              current={entry === file}
              onChoose={() => setChosen(entry.path)}
            />
          ))}
          <p style={{ margin: "12px 0 0", padding: "0 10px", fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>
            {t("proposal.filesHint")}
          </p>
        </nav>

        <FileView
          key={file.path}
          repository={repository}
          id={id}
          page={page}
          file={file}
          comments={page.comments.filter((comment) => comment.path === file.path)}
          onEnlarge={setEnlarged}
        />
      </div>

      {enlarged ? <ImageViewer picture={enlarged} onClose={() => setEnlarged(null)} /> : null}
    </div>
  );
}

/** The strip above the proposal: what it is and where it was read, and the decisions on the right. */
function Bar({ decide, children }: { decide: ReactNode; children: ReactNode }) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 16,
        flexWrap: "wrap",
        padding: "12px 16px",
        border: "1px solid var(--border)",
        borderRadius: "var(--radius-lg)",
        fontSize: "var(--text-sm)",
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 14,
          flexWrap: "wrap",
          color: "var(--muted-foreground)",
          minWidth: 0,
        }}
      >
        {children}
      </div>
      {decide}
    </div>
  );
}

/** The issue's latest `## Plan` comment as written, with who wrote it, when, and the earlier ones linked. */
function PlanView({
  repository,
  id,
  page,
  plan,
  decide,
  hint,
  onEnlarge,
  children,
}: {
  repository: string;
  id: string;
  page: ProposalPage;
  plan: ProposalPlan;
  decide: ReactNode;
  hint: ReactNode;
  onEnlarge: (picture: Picture) => void;
  children: ReactNode;
}) {
  const assets = new Set(plan.images);
  const image = (src: string) => (assets.has(src) ? attachmentUrl(repository, id, src) : src);
  const earlier = page.earlierPlans;
  const by = t("proposal.planBy", { author: plan.author ?? t("issue.ghost"), when: when(plan.createdAt) });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16, maxWidth: 1100, minWidth: 0 }}>
      <Bar decide={decide}>
        {plan.url ? (
          <a href={plan.url} target="_blank" rel="noreferrer" style={{ color: "inherit" }}>
            {by}
          </a>
        ) : (
          <span>{by}</span>
        )}
        {page.pullRequestId ? <span>{t("proposal.planPr", { pr: page.pullRequestId })}</span> : null}
        {earlier.length > 0 ? (
          <span style={{ display: "inline-flex", gap: 8, flexWrap: "wrap" }}>
            {t("proposal.planEarlier", { count: String(earlier.length) })}
            {earlier.map((before) =>
              before.url ? (
                <a key={before.id} href={before.url} target="_blank" rel="noreferrer" style={{ color: "inherit" }}>
                  {when(before.createdAt)}
                </a>
              ) : (
                <span key={before.id}>{when(before.createdAt)}</span>
              ),
            )}
          </span>
        ) : null}
      </Bar>
      {hint}
      <article
        aria-label={t("proposal.planLabel")}
        style={{
          padding: "16px 20px",
          border: "1px solid var(--border)",
          borderRadius: "var(--radius-lg)",
          minWidth: 0,
        }}
      >
        <VendorMarkdown text={plan.body} image={image} onEnlarge={onEnlarge} />
      </article>
      {children}
    </div>
  );
}

/** Approve, and Request changes when the state has a `back` to send the PBI to. */
function Decide({
  repository,
  id,
  held,
  back,
  page,
}: {
  repository: string;
  id: string;
  held: boolean;
  back: string | null;
  page: ProposalPage;
}) {
  const release = usePbiAction(repository, id);
  const request = useRequestProposalChanges(repository, id);
  const [asking, setAsking] = useState(false);
  const [body, setBody] = useState("");
  const [unmoved, setUnmoved] = useState<string | null>(null);

  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
      <Button
        variant="outline"
        size="sm"
        disabled={!held || release.isPending}
        title={held ? undefined : t("proposal.approveNotHeld")}
        onClick={() => release.mutate({ kind: "release" })}
      >
        {t("proposal.approve")}
      </Button>
      {back ? (
        <Button
          variant="outline"
          size="sm"
          onClick={() => {
            request.reset();
            setAsking(true);
          }}
        >
          {t("proposal.requestChanges")}
        </Button>
      ) : null}
      {unmoved ? (
        <span role="status" style={{ flexBasis: "100%", fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>
          {t("review.unmoved", { reason: unmoved })}
        </span>
      ) : null}
      {release.error ? (
        <span role="alert" style={{ flexBasis: "100%", fontSize: "var(--text-xs)", color: "var(--destructive)" }}>
          {t("calls.pbi.refused", { reason: explain(release.error) })}
        </span>
      ) : null}

      {asking ? (
        <Dialog
          title={t("proposal.requestChanges")}
          onClose={() => setAsking(false)}
          width={Math.min(520, window.innerWidth - 32)}
          footer={
            <>
              {request.error ? (
                <span role="alert" style={{ fontSize: "var(--text-xs)", color: "var(--destructive)" }}>
                  {t("calls.pbi.refused", { reason: explain(request.error) })}
                </span>
              ) : null}
              <div style={{ flex: 1 }} />
              <Button variant="outline" size="sm" onClick={() => setAsking(false)}>
                {t("calls.pbi.cancel")}
              </Button>
              <Button
                variant="destructive"
                size="sm"
                disabled={body.trim() === "" || request.isPending}
                onClick={() =>
                  request.mutate(body, {
                    onSuccess: (answer) => {
                      setAsking(false);
                      setBody("");
                      setUnmoved(answer.unmoved);
                    },
                  })
                }
              >
                {t("proposal.sendReview")}
              </Button>
            </>
          }
        >
          <p style={{ margin: 0, color: "var(--muted-foreground)" }}>
            {page.plan
              ? t("proposal.requestBodyPlan", { pr: page.pullRequestId ?? "", back: back ?? "" })
              : t("proposal.requestBody", {
                  pr: page.pullRequestId ?? "",
                  threads: String(threads(page.comments).length),
                  back: back ?? "",
                })}
          </p>
          <Field label={t("proposal.reviewField")}>
            <textarea
              className="pc-textarea"
              rows={4}
              value={body}
              onChange={(event) => setBody(event.target.value)}
              style={{ resize: "vertical" }}
            />
          </Field>
        </Dialog>
      ) : null}
    </div>
  );
}

function FileLink({
  file,
  threads,
  current,
  onChoose,
}: {
  file: ProposalFile;
  threads: number;
  current: boolean;
  onChoose: () => void;
}) {
  const side =
    threads > 0 ? (
      <Badge variant="warning">
        <span aria-label={t("proposal.openThreads", { count: String(threads) })}>{threads}</span>
      </Badge>
    ) : file.delta ? (
      <span>{deltaWords(file.delta)}</span>
    ) : file.tasks ? (
      <span>{t("proposal.taskCount", { done: String(file.tasks.done), total: String(file.tasks.total) })}</span>
    ) : null;

  return (
    <button
      type="button"
      className="aio-proposal-file aio-journey-run"
      aria-current={current ? "page" : undefined}
      onClick={onChoose}
      style={{
        display: "flex",
        justifyContent: "space-between",
        gap: 8,
        padding: "7px 10px",
        border: 0,
        borderRadius: "var(--radius)",
        background: current ? "var(--accent)" : undefined,
        color: current ? "var(--foreground)" : "var(--muted-foreground)",
        fontWeight: current ? 500 : 400,
        font: "inherit",
        fontSize: "var(--text-sm)",
        textAlign: "left",
        cursor: "pointer",
      }}
    >
      <span style={{ fontFamily: "var(--font-mono)", overflowWrap: "anywhere" }}>{file.name}</span>
      <span style={{ fontSize: "var(--text-xs)", whiteSpace: "nowrap" }}>{side}</span>
    </button>
  );
}

function deltaWords(delta: NonNullable<ProposalFile["delta"]>): string {
  return [
    delta.added > 0 ? `+${delta.added}` : null,
    delta.modified > 0 ? `~${delta.modified}` : null,
    delta.removed > 0 ? `−${delta.removed}` : null,
    delta.renamed > 0 ? `→${delta.renamed}` : null,
  ]
    .filter(Boolean)
    .join(" ");
}

/** The comments of one file grouped by the line they are on, the first of each the thread's start. */
function threads(comments: ProposalComment[]): ProposalComment[][] {
  const byLine = new Map<string, ProposalComment[]>();
  for (const comment of comments) {
    const key = `${comment.path}:${comment.line ?? "outdated"}`;
    byLine.set(key, [...(byLine.get(key) ?? []), comment]);
  }
  return [...byLine.values()];
}

/** One file of the change: an image drawn whole, or markdown cut into numbered pieces with their threads. */
function FileView({
  repository,
  id,
  page,
  file,
  comments,
  onEnlarge,
}: {
  repository: string;
  id: string;
  page: ProposalPage;
  file: ProposalFile;
  comments: ProposalComment[];
  onEnlarge: (picture: Picture) => void;
}) {
  const [composing, setComposing] = useState<number | null>(null);
  const [master, setMaster] = useState(false);
  const paths = new Set(page.files.map((entry) => entry.path));
  const image = (src: string) => {
    if (/^[a-z]+:/i.test(src)) return src;
    const path = resolve(file.path, src);
    return paths.has(path) ? proposalFileUrl(repository, id, path) : src;
  };

  const pieces = file.text === null ? [] : blocks(file.text);
  const outdated = comments.filter((comment) => comment.line === null);

  return (
    <article
      aria-label={file.name}
      style={{
        display: "flex",
        flexDirection: "column",
        border: "1px solid var(--border)",
        borderRadius: "var(--radius-lg)",
        overflow: "hidden",
        minWidth: 0,
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 10,
          flexWrap: "wrap",
          padding: "8px 16px",
          borderBottom: "1px solid var(--border)",
          fontFamily: "var(--font-mono)",
          fontSize: "var(--text-xs)",
          color: "var(--muted-foreground)",
        }}
      >
        <span style={{ overflowWrap: "anywhere" }}>{file.name}</span>
        {file.delta ? (
          <Badge variant="outline">
            {t("proposal.delta", {
              added: String(file.delta.added),
              modified: String(file.delta.modified),
              removed: String(file.delta.removed),
            })}
          </Badge>
        ) : null}
        <span style={{ marginLeft: "auto", fontFamily: "var(--font-sans)" }}>
          {file.kind === "image" ? t("proposal.image") : t("proposal.rendered")}
        </span>
      </div>

      {file.kind === "image" ? (
        <div style={{ padding: 16 }}>
          <VendorMarkdown text={`![${file.name}](${file.name})`} image={image} onEnlarge={onEnlarge} />
        </div>
      ) : file.text === null ? (
        <p style={{ margin: 16, color: "var(--muted-foreground)" }}>{t("proposal.unread")}</p>
      ) : (
        <ol aria-label={t("proposal.lines", { name: file.name })} style={{ listStyle: "none", margin: 0, padding: "8px 0" }}>
          {pieces.map((piece) => (
            <Piece
              key={piece.line}
              piece={piece}
              comments={comments.filter((comment) => comment.line !== null && comment.line >= piece.line && comment.line <= piece.end)}
              composing={composing === piece.line}
              onCompose={(open) => setComposing(open ? piece.line : null)}
              repository={repository}
              id={id}
              path={file.path}
              image={image}
              onEnlarge={onEnlarge}
            />
          ))}
        </ol>
      )}

      {outdated.length > 0 ? (
        <div style={{ padding: "8px 16px 16px", display: "flex", flexDirection: "column", gap: 8 }}>
          <span style={{ fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>{t("proposal.outdated")}</span>
          <Thread comments={outdated} />
        </div>
      ) : null}

      {file.capability ? (
        <div style={{ borderTop: "1px solid var(--border)", padding: "10px 16px", display: "flex", flexDirection: "column", gap: 10 }}>
          {file.master === null ? (
            <span style={{ fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>
              {t("proposal.newCapability", { capability: file.capability })}
            </span>
          ) : (
            <>
              <Button
                variant="ghost"
                size="sm"
                aria-expanded={master}
                onClick={() => setMaster((open) => !open)}
                style={{ alignSelf: "flex-start" }}
              >
                {master
                  ? t("proposal.hideMaster", { capability: file.capability })
                  : t("proposal.showMaster", { capability: file.capability })}
              </Button>
              {master ? (
                <section aria-label={t("proposal.masterLabel", { capability: file.capability })} style={{ color: "var(--muted-foreground)" }}>
                  <VendorMarkdown text={file.master} image={(src) => src} onEnlarge={onEnlarge} />
                </section>
              ) : null}
            </>
          )}
        </div>
      ) : null}
    </article>
  );
}

/** One numbered piece of a file, the button that anchors a comment to it, and its threads. */
function Piece({
  piece,
  comments,
  composing,
  onCompose,
  repository,
  id,
  path,
  image,
  onEnlarge,
}: {
  piece: Block;
  comments: ProposalComment[];
  composing: boolean;
  onCompose: (open: boolean) => void;
  repository: string;
  id: string;
  path: string;
  image: (src: string) => string;
  onEnlarge: (picture: Picture) => void;
}) {
  const grouped = threads(comments);

  return (
    <li>
      <div
        className={`aio-proposal-line${grouped.length > 0 ? " aio-proposal-line--threaded" : ""}`}
        style={{
          display: "grid",
          gridTemplateColumns: "44px minmax(0, 1fr) 32px",
          gap: "0 12px",
          padding: "4px 8px 4px 16px",
          alignItems: "start",
        }}
      >
        <span
          style={{
            fontFamily: "var(--font-mono)",
            fontSize: 11,
            color: "var(--muted-foreground)",
            textAlign: "right",
            paddingTop: 3,
          }}
        >
          {piece.line}
        </span>
        <div style={{ minWidth: 0, paddingLeft: piece.indent * 16 }}>
          <VendorMarkdown text={piece.text} image={image} onEnlarge={onEnlarge} />
        </div>
        <Button
          variant="ghost"
          size="icon-xs"
          className="aio-proposal-anchor"
          aria-label={t("proposal.commentOn", { line: String(piece.line) })}
          title={t("proposal.commentOn", { line: String(piece.line) })}
          onClick={() => onCompose(!composing)}
        >
          <MessageSquarePlus size={14} aria-hidden />
        </Button>
      </div>

      {grouped.map((thread) => (
        <div key={thread[0]?.id} style={{ margin: "4px 16px 8px 72px" }}>
          <Thread comments={thread} />
          <Reply repository={repository} id={id} path={path} line={thread[0]?.line ?? piece.line} />
        </div>
      ))}

      {composing ? (
        <div style={{ margin: "4px 16px 8px 72px" }}>
          <Anchor repository={repository} id={id} path={path} line={piece.line} onDone={() => onCompose(false)} />
        </div>
      ) : null}
    </li>
  );
}

function Thread({ comments }: { comments: ProposalComment[] }) {
  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        gap: 8,
        padding: "10px 14px",
        border: "1px solid var(--border)",
        borderRadius: "var(--radius)",
        fontSize: "var(--text-sm)",
        background: "var(--card)",
      }}
    >
      {comments.map((comment, index) => (
        <div
          key={comment.id}
          style={index === 0 ? undefined : { paddingLeft: 12, borderLeft: "2px solid var(--border)" }}
        >
          <div style={{ fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>
            <strong style={{ color: "var(--foreground)" }}>{comment.author ?? t("issue.ghost")}</strong>{" "}
            {t("proposal.commentMeta", {
              when: when(comment.createdAt),
              line: comment.line === null ? "—" : String(comment.line),
            })}
          </div>
          <VendorMarkdown text={comment.body} image={(src) => src} onEnlarge={() => undefined} />
        </div>
      ))}
    </div>
  );
}

/** A new comment on one line: a review comment on the pull request at that file and line. */
function Anchor({
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
  const write = useProposalComment(repository, id);
  const [body, setBody] = useState("");

  return (
    <form
      aria-label={t("proposal.commentOn", { line: String(line) })}
      onSubmit={(event) => {
        event.preventDefault();
        if (body.trim() !== "") write.mutate({ path, line, body }, { onSuccess: onDone });
      }}
      style={{ display: "flex", flexDirection: "column", gap: 8 }}
    >
      <textarea
        className="pc-textarea"
        rows={3}
        autoFocus
        value={body}
        aria-label={t("proposal.commentField", { line: String(line) })}
        placeholder={t("proposal.commentPlaceholder")}
        onChange={(event) => setBody(event.target.value)}
        style={{ resize: "vertical" }}
      />
      {write.error ? (
        <span role="alert" style={{ fontSize: "var(--text-xs)", color: "var(--destructive)" }}>
          {t("calls.pbi.refused", { reason: explain(write.error) })}
        </span>
      ) : null}
      <div style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}>
        <Button variant="outline" size="sm" onClick={onDone}>
          {t("calls.pbi.cancel")}
        </Button>
        <Button type="submit" size="sm" disabled={body.trim() === "" || write.isPending}>
          {t("proposal.comment")}
        </Button>
      </div>
    </form>
  );
}

/** A reply to a thread: one more review comment on the same line. */
function Reply({ repository, id, path, line }: { repository: string; id: string; path: string; line: number }) {
  const write = useProposalComment(repository, id);
  const [body, setBody] = useState("");

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        if (body.trim() !== "") write.mutate({ path, line, body }, { onSuccess: () => setBody("") });
      }}
      style={{ display: "flex", gap: 8, marginTop: 6, flexWrap: "wrap" }}
    >
      <input
        className="pc-input"
        value={body}
        aria-label={t("proposal.reply", { line: String(line) })}
        placeholder={t("proposal.reply", { line: String(line) })}
        onChange={(event) => setBody(event.target.value)}
        style={{ flex: "1 1 200px", minWidth: 0 }}
      />
      <Button type="submit" variant="outline" size="sm" disabled={body.trim() === "" || write.isPending}>
        {t("proposal.replyButton")}
      </Button>
      {write.error ? (
        <span role="alert" style={{ flexBasis: "100%", fontSize: "var(--text-xs)", color: "var(--destructive)" }}>
          {t("calls.pbi.refused", { reason: explain(write.error) })}
        </span>
      ) : null}
    </form>
  );
}
