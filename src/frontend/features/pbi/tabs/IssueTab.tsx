import { ExternalLink, ImagePlus, X } from "lucide-react";
import { useCallback, useMemo, useRef, useState, type ReactNode } from "react";
import { Link } from "react-router";
import { explain } from "@/shared/http/client";
import {
  attachmentUrl,
  useIssue,
  useIssueChildren,
  useIssueComment,
  useLoopPresent,
  useRepositoryJournal,
  type DroppedImage,
  type IssueComment,
  type IssuePage,
} from "@/shared/http/queries";
import { t } from "@/shared/i18n";
import { Badge, Button, ErrorState, Loading } from "@/shared/ui/kit";
import { Mermaid } from "@/shared/ui/Mermaid";
import {
  basicDiagram,
  countChildren,
  liveChildren,
  overlay,
  type LiveChild,
  type StatusPalette,
} from "@/shared/ui/mermaidOverlay";
import { useNarrowContainer } from "@/shared/ui/useNarrowContainer";
import { when } from "../format";
import { ImageViewer } from "../issue/ImageViewer";
import { VendorMarkdown, type Picture } from "../issue/VendorMarkdown";
import type { PbiTabProps } from "../PbiTabProps";

/**
 * The Issue tab (DESIGN.md#the-screens, #512): what was asked, and what has been said since. The
 * issue as the vendor has it — its body with its images inside, and every comment in order, the
 * harness's marked as what they are with their captures — and a composer that publishes at the
 * vendor as the person, with the images they drop. Beside it, what the issue says about itself.
 *
 * Nothing here edits the body: that is the vendor's page, one click away.
 */
export default function IssueTab({ folder, repository, id, detail }: PbiTabProps) {
  const issue = useIssue(repository, id);
  const [measure, narrow] = useNarrowContainer<HTMLDivElement>(820);
  const [enlarged, setEnlarged] = useState<Picture | null>(null);
  const { children, diagram } = useEpicGraph(repository, id);

  if (issue.error)
    return (
      <ErrorState
        detail={t("issue.failed", { reason: explain(issue.error) })}
        onRetry={() => void issue.refetch()}
      />
    );
  if (!issue.data) return <Loading />;

  const page = issue.data;
  const assets = new Set(page.images.map((image) => image.url));
  const image = (src: string) => (assets.has(src) ? attachmentUrl(repository, id, src) : src);

  return (
    <div
      ref={measure}
      style={{
        display: "grid",
        gridTemplateColumns: narrow ? "minmax(0, 1fr)" : "minmax(0, 1fr) 260px",
        gap: narrow ? 24 : 32,
        maxWidth: 1100,
        alignItems: "start",
      }}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: 16, minWidth: 0 }}>
        <Card>
          <Byline
            author={page.author}
            meta={t("issue.opened", { when: when(page.createdAt) })}
            end={
              <a
                href={page.url}
                target="_blank"
                rel="noreferrer"
                style={{ marginLeft: "auto", display: "inline-flex", alignItems: "center", gap: 4 }}
              >
                {t("issue.openAtVendor")}
                <ExternalLink size={12} aria-hidden />
              </a>
            }
          />
          {page.body.trim() === "" ? (
            <p style={{ margin: 0, color: "var(--muted-foreground)" }}>{t("issue.noBody")}</p>
          ) : (
            <VendorMarkdown text={page.body} image={image} onEnlarge={setEnlarged} diagram={diagram} />
          )}
        </Card>

        {children.length > 0 ? (
          <Card>
            <h2 style={{ margin: 0, fontSize: "var(--text-xs)", fontWeight: 600, color: "var(--muted-foreground)" }}>
              {t("issue.graph", { count: String(children.length) })}
            </h2>
            <ChildCountList subIssues={children} />
            {MERMAID_BLOCK.test(page.body) ? null : (
              <Mermaid text={basicDiagram(id, detail.pbi.title, children)} transform={diagram} />
            )}
          </Card>
        ) : null}

        <h2
          style={{
            margin: "4px 0 -4px",
            fontSize: "var(--text-xs)",
            fontWeight: 600,
            color: "var(--muted-foreground)",
          }}
        >
          {t("issue.comments", { count: String(page.comments.length) })}
        </h2>

        {page.comments.length === 0 ? (
          <p style={{ margin: 0, color: "var(--muted-foreground)" }}>{t("issue.noComments")}</p>
        ) : (
          <ol
            aria-label={t("issue.commentsLabel")}
            style={{ listStyle: "none", margin: 0, padding: 0, display: "flex", flexDirection: "column", gap: 12 }}
          >
            {page.comments.map((comment) => (
              <li key={comment.id}>
                <Comment comment={comment} page={page} image={image} onEnlarge={setEnlarged} />
              </li>
            ))}
          </ol>
        )}

        <Composer repository={repository} id={id} />
      </div>

      <Facts page={page} folderId={folder.id} repository={repository} image={image} onEnlarge={setEnlarged} />

      {enlarged ? <ImageViewer picture={enlarged} onClose={() => setEnlarged(null)} /> : null}
    </div>
  );
}

const MERMAID_BLOCK = /^\s*(```|~~~)\s*mermaid\b/m;

/**
 * #611. The issue's sub-issues with their live state, and the rewrite that paints them on a Mermaid
 * graph: what the vendor says of each (closed, held, its state), what the loop is running, and the
 * newest journal line of each, which says whether it waits and, on files, for whom. Without
 * sub-issues, or while they are read, the rewrite leaves a diagram as it is.
 */
function useEpicGraph(repository: string, id: string) {
  const read = useIssueChildren(repository, id);
  const any = (read.data?.length ?? 0) > 0;
  const present = useLoopPresent();
  const journal = useRepositoryJournal(repository, any);

  const children = useMemo(
    () =>
      liveChildren(
        read.data ?? [],
        { liveRuns: present.data?.liveRuns ?? [], liveScripts: present.data?.liveScripts ?? [] },
        journal.data ?? [],
      ),
    [read.data, present.data, journal.data],
  );
  const diagram = useCallback(
    (text: string, palette: StatusPalette) => overlay(text, children, palette, t("mermaid.waits")),
    [children],
  );
  return { children, diagram };
}

/**
 * #636. How many sub-issues are in each live state, in the graph's colours: done, running, waiting
 * and held always, and the rest at a plain loop state only when there are any.
 */
function ChildCountList({ subIssues }: { subIssues: readonly LiveChild[] }) {
  const counts = countChildren(subIssues);
  const shown = [
    ["done", "success"],
    ["running", "default"],
    ["waiting", "outline"],
    ["held", "warning"],
    ...(counts.other > 0 ? [["other", "secondary"] as const] : []),
  ] as const;

  return (
    <ul
      aria-label={t("issue.counts")}
      style={{ listStyle: "none", margin: 0, padding: 0, display: "flex", flexWrap: "wrap", gap: 6 }}
    >
      {shown.map(([status, variant]) => (
        <li key={status}>
          <Badge variant={variant}>{t(`issue.count.${status}`, { count: String(counts[status]) })}</Badge>
        </li>
      ))}
    </ul>
  );
}

function Card({ children, harness = false }: { children: ReactNode; harness?: boolean }) {
  return (
    <article
      style={{
        display: "flex",
        flexDirection: "column",
        gap: 10,
        padding: "14px 20px",
        border: "1px solid var(--border)",
        borderRadius: "var(--radius-lg)",
        background: harness ? "var(--muted)" : "var(--card)",
        minWidth: 0,
      }}
    >
      {children}
    </article>
  );
}

function Byline({ author, meta, end }: { author: string | null; meta: string; end?: ReactNode }) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 10,
        flexWrap: "wrap",
        fontSize: "var(--text-xs)",
        color: "var(--muted-foreground)",
      }}
    >
      <strong style={{ color: "var(--foreground)" }}>{author ?? t("issue.ghost")}</strong>
      <span>{meta}</span>
      {end}
    </div>
  );
}

/**
 * One comment. A person's is drawn as written; the harness's is marked with the kind of record it
 * carries, and its images are drawn as a grid of captures rather than one under another.
 */
function Comment({
  comment,
  page,
  image,
  onEnlarge,
}: {
  comment: IssueComment;
  page: IssuePage;
  image: (src: string) => string;
  onEnlarge: (picture: Picture) => void;
}) {
  const captures = comment.harness ? page.images.filter((entry) => entry.commentId === comment.id) : [];
  const text = comment.harness ? withoutImages(comment.body) : comment.body;

  return (
    <Card harness={comment.harness !== null}>
      <Byline
        author={comment.author}
        meta={when(comment.createdAt)}
        end={
          comment.harness ? (
            <span style={{ marginLeft: "auto" }}>
              <Badge variant="secondary">{t("issue.harness", { kind: comment.harness })}</Badge>
            </span>
          ) : null
        }
      />
      {text.trim() === "" ? null : <VendorMarkdown text={text} image={image} onEnlarge={onEnlarge} />}
      {captures.length > 0 ? (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(180px, 1fr))", gap: 10 }}>
          {captures.map((capture) => (
            <figure key={capture.url} style={{ margin: 0, display: "flex", flexDirection: "column", gap: 4, minWidth: 0 }}>
              <button
                type="button"
                className="aio-image"
                aria-label={t("image.enlarge", { name: capture.name || t("image.unnamed") })}
                onClick={() => onEnlarge({ src: image(capture.url), alt: capture.name })}
              >
                <img
                  src={image(capture.url)}
                  alt={capture.name}
                  loading="lazy"
                  style={{ width: "100%", height: 120, objectFit: "cover", objectPosition: "top" }}
                />
              </button>
              <figcaption
                style={{
                  fontSize: "var(--text-xs)",
                  color: "var(--muted-foreground)",
                  fontFamily: "var(--font-mono)",
                  overflowWrap: "anywhere",
                }}
              >
                {capture.name}
              </figcaption>
            </figure>
          ))}
        </div>
      ) : null}
    </Card>
  );
}

/** A comment's text with its images taken out, for the harness's, whose images are drawn as a grid. */
function withoutImages(body: string): string {
  return body.replace(/!\[[^\]]*\]\([^)]*\)/g, "").replace(/<img\b[^>]*>/gi, "");
}

/** What the issue says about itself: where it comes from, who has it, its labels and links, its images. */
function Facts({
  page,
  folderId,
  repository,
  image,
  onEnlarge,
}: {
  page: IssuePage;
  folderId: string;
  repository: string;
  image: (src: string) => string;
  onEnlarge: (picture: Picture) => void;
}) {
  const link = (entry: IssuePage["blockedBy"][number]) => {
    const label = t("issue.linked", { id: entry.id, title: entry.title });
    const closed = entry.closed ? ` ${t("issue.closed")}` : "";
    return entry.repository.toLowerCase() === repository.toLowerCase() ? (
      <Link key={entry.id} to={`/pbis/${folderId}/${entry.id}/issue`}>
        {label}
        {closed}
      </Link>
    ) : (
      <a key={`${entry.repository}#${entry.id}`} href={entry.url} target="_blank" rel="noreferrer">
        {`${entry.repository}${label}`}
        {closed}
      </a>
    );
  };

  return (
    <aside
      aria-label={t("issue.facts")}
      style={{ display: "flex", flexDirection: "column", gap: 16, fontSize: "var(--text-sm)", minWidth: 0 }}
    >
      <Fact label={t("issue.source")}>
        <span style={{ overflowWrap: "anywhere" }}>{t("issue.sourceValue", { repository })}</span>
      </Fact>
      <Fact label={t("issue.assignee")}>
        <span>{page.assignees.length === 0 ? t("issue.nobody") : page.assignees.join(", ")}</span>
      </Fact>
      <Fact label={t("issue.labels")}>
        {page.labels.length === 0 ? (
          <span style={{ color: "var(--muted-foreground)" }}>{t("issue.none")}</span>
        ) : (
          <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
            {page.labels.map((label) => (
              <Badge key={label} variant="outline" style={{ fontFamily: "var(--font-mono)" }}>
                {label}
              </Badge>
            ))}
          </div>
        )}
      </Fact>
      <Fact label={t("issue.parent")}>
        {page.parent ? link(page.parent) : <span style={{ color: "var(--muted-foreground)" }}>{t("issue.none")}</span>}
      </Fact>
      <Fact label={t("issue.blockedBy")}>
        {page.blockedBy.length === 0 ? (
          <span style={{ color: "var(--muted-foreground)" }}>{t("issue.none")}</span>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>{page.blockedBy.map(link)}</div>
        )}
      </Fact>
      <Fact label={t("issue.images", { count: String(page.images.length) })}>
        {page.images.length === 0 ? (
          <span style={{ color: "var(--muted-foreground)" }}>{t("issue.none")}</span>
        ) : (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(3, minmax(0, 1fr))", gap: 6 }}>
            {page.images.map((entry) => (
              <button
                key={entry.url}
                type="button"
                className="aio-image"
                aria-label={t("image.enlarge", { name: entry.name || t("image.unnamed") })}
                onClick={() => onEnlarge({ src: image(entry.url), alt: entry.name })}
              >
                <img
                  src={image(entry.url)}
                  alt=""
                  loading="lazy"
                  style={{ width: "100%", height: 48, objectFit: "cover", objectPosition: "top" }}
                />
              </button>
            ))}
          </div>
        )}
      </Fact>
    </aside>
  );
}

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <span style={{ fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>{label}</span>
      {children}
    </div>
  );
}

/**
 * The comment a person writes here, published at the vendor as them. Images are dropped, pasted or
 * chosen, and travel with the text; the vendor uploads them and links them into the comment.
 */
function Composer({ repository, id }: { repository: string; id: string }) {
  const comment = useIssueComment(repository, id);
  const [body, setBody] = useState("");
  const [images, setImages] = useState<DroppedImage[]>([]);
  const [dropping, setDropping] = useState(false);
  const chooser = useRef<HTMLInputElement>(null);

  const add = async (files: FileList | File[]) => {
    const read = await Promise.all(
      [...files].filter((file) => file.type.startsWith("image/") || file.type.startsWith("video/")).map(asDropped),
    );
    setImages((current) => [...current, ...read]);
  };

  const send = () =>
    comment.mutate(
      { body, attachments: images },
      {
        onSuccess: () => {
          setBody("");
          setImages([]);
        },
      },
    );

  const empty = body.trim() === "" && images.length === 0;

  return (
    <form
      aria-label={t("issue.composer")}
      className={dropping ? "aio-composer--dropping" : undefined}
      onSubmit={(event) => {
        event.preventDefault();
        if (!empty) send();
      }}
      onDragOver={(event) => {
        if ([...event.dataTransfer.types].includes("Files")) {
          event.preventDefault();
          setDropping(true);
        }
      }}
      onDragLeave={() => setDropping(false)}
      onDrop={(event) => {
        event.preventDefault();
        setDropping(false);
        void add(event.dataTransfer.files);
      }}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: 8,
        padding: "14px 20px",
        border: "1px solid var(--border)",
        borderRadius: "var(--radius-lg)",
      }}
    >
      <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
        <span style={{ fontSize: "var(--text-sm)", fontWeight: 500 }}>
          {t("issue.comment")}{" "}
          <span style={{ fontWeight: 400, color: "var(--muted-foreground)" }}>
            {t("issue.commentAs", { id })}
          </span>
        </span>
        <textarea
          className="pc-textarea"
          rows={3}
          value={body}
          placeholder={t("issue.commentPlaceholder")}
          onChange={(event) => setBody(event.target.value)}
          onPaste={(event) => {
            if (event.clipboardData.files.length > 0) {
              event.preventDefault();
              void add(event.clipboardData.files);
            }
          }}
          style={{ resize: "vertical" }}
        />
      </label>

      {images.length > 0 ? (
        <ul
          aria-label={t("issue.attached")}
          style={{ listStyle: "none", margin: 0, padding: 0, display: "flex", flexWrap: "wrap", gap: 6 }}
        >
          {images.map((dropped, index) => (
            <li key={`${dropped.name}-${index}`}>
              <Badge variant="outline">
                <span style={{ fontFamily: "var(--font-mono)" }}>{dropped.name}</span>
                <Button
                  variant="ghost"
                  size="icon-xs"
                  aria-label={t("issue.removeImage", { name: dropped.name })}
                  onClick={() => setImages((current) => current.filter((_, at) => at !== index))}
                >
                  <X size={12} aria-hidden />
                </Button>
              </Badge>
            </li>
          ))}
        </ul>
      ) : null}

      {comment.error ? (
        <span role="alert" style={{ fontSize: "var(--text-xs)", color: "var(--destructive)" }}>
          {t("issue.commentRefused", { reason: explain(comment.error) })}
        </span>
      ) : null}

      <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
        <input
          ref={chooser}
          type="file"
          accept="image/*,video/*"
          multiple
          hidden
          onChange={(event) => {
            if (event.target.files) void add(event.target.files);
            event.target.value = "";
          }}
        />
        <Button variant="ghost" size="sm" onClick={() => chooser.current?.click()}>
          <ImagePlus size={14} aria-hidden />
          {t("issue.addImage")}
        </Button>
        <div style={{ flex: 1 }} />
        <Button type="submit" size="sm" disabled={empty || comment.isPending}>
          {comment.isPending ? t("issue.commenting") : t("issue.send")}
        </Button>
      </div>
    </form>
  );
}

function asDropped(file: File): Promise<DroppedImage> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const url = String(reader.result);
      resolve({ name: file.name || "image.png", base64: url.slice(url.indexOf(",") + 1) });
    };
    reader.onerror = () => reject(reader.error ?? new Error(file.name));
    reader.readAsDataURL(file);
  });
}
