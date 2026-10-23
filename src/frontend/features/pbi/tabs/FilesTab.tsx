import { ChevronDown, ChevronRight } from "lucide-react";
import { createContext, useContext, useEffect, useMemo, useRef, useState, type KeyboardEvent } from "react";
import { Tree, type NodeRendererProps, type TreeApi } from "react-arborist";
import { explain } from "@/shared/http/client";
import {
  useOpenPbiFile,
  usePbiFile,
  usePbiFiles,
  useSavePbiFile,
  type PbiFiles,
  type PbiFilesScope,
} from "@/shared/http/queries";
import { t } from "@/shared/i18n";
import { CodeEditor, languageOf } from "@/shared/ui/CodeEditor";
import { Button, Callout, ErrorState, Loading } from "@/shared/ui/kit";
import { useNarrowContainer } from "@/shared/ui/useNarrowContainer";
import type { PbiTabProps } from "../PbiTabProps";
import { fileTree, type FileEntry } from "./files/fileTree";

/**
 * The Files tab (DESIGN.md#files, #514): the repository on the PBI's branch. The
 * tree is react-arborist, virtualised, so a repository of thousands of files draws only the rows on
 * screen; the chosen file is CodeMirror 6. Save writes to disk in the checkout that holds the
 * branch and commits nothing; Open in editor hands the file to the editor Settings names.
 *
 * When the repository no longer has the branch, or no checkout holds it, the tab says so and
 * offers the folder's checkout as it is. It is not an IDE: no terminal, no run, no language server.
 */
export default function FilesTab({ folder, id }: PbiTabProps) {
  const [asIs, setAsIs] = useState(false);
  const branchScope = useMemo(() => ({ folderId: folder.id, pbi: id, asIs: false }), [folder.id, id]);
  const checkoutScope = useMemo(() => ({ folderId: folder.id, pbi: id, asIs: true }), [folder.id, id]);
  const branch = usePbiFiles(branchScope);
  const checkout = usePbiFiles(checkoutScope, asIs);
  const shown = asIs ? checkout : branch;
  const [measure, narrow] = useNarrowContainer<HTMLDivElement>(720);

  let body;
  if (shown.error)
    body = (
      <ErrorState
        detail={t("files.failed", { reason: explain(shown.error) })}
        onRetry={() => void shown.refetch()}
      />
    );
  else if (!shown.data) body = <Loading />;
  else
    body = (
      <Browser
        key={asIs ? "as-is" : "branch"}
        scope={asIs ? checkoutScope : branchScope}
        files={shown.data}
        narrow={narrow}
      />
    );

  const wanted = branch.data?.branch ?? "";
  return (
    <div ref={measure} style={{ display: "flex", flexDirection: "column", gap: 12, minWidth: 0 }}>
      {asIs ? (
        <Callout tone="info">
          <span style={{ flex: 1 }}>
            {t("files.asIs", { branch: checkout.data?.branch ?? t("files.detached") })}
          </span>
          <Button variant="outline" size="sm" onClick={() => setAsIs(false)}>
            {t("files.backToBranch")}
          </Button>
        </Callout>
      ) : branch.data && (!branch.data.exists || !branch.data.checkout) ? (
        <Callout>
          <span style={{ flex: 1 }}>
            {branch.data.exists
              ? t("files.noCheckout", { branch: wanted })
              : t("files.gone", { branch: wanted })}
          </span>
          <Button variant="outline" size="sm" onClick={() => setAsIs(true)}>
            {t("files.showCheckout")}
          </Button>
        </Callout>
      ) : null}
      {branch.data && !branch.data.exists && !asIs ? null : body}
    </div>
  );
}

/** Which files are modified, for the tree's rows, which react-arborist renders as a component of its own. */
const Modified = createContext<ReadonlySet<string>>(new Set());

function Browser({ scope, files, narrow }: { scope: PbiFilesScope; files: PbiFiles; narrow: boolean }) {
  const [chosen, setChosen] = useState<string | null>(null);
  const [filter, setFilter] = useState("");
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [note, setNote] = useState<{ text: string; failed: boolean } | null>(null);
  const tree = useMemo(() => fileTree(files.files), [files.files]);
  const modified = useMemo(() => new Set(files.modified), [files.modified]);
  const file = usePbiFile(scope, chosen);
  const save = useSavePbiFile(scope);
  const open = useOpenPbiFile(scope);
  const api = useRef<TreeApi<FileEntry>>(null);
  const [box, size] = useSize<HTMLDivElement>();

  // A filter shortens the list, and a list scrolled past its new end draws nothing: back to the top.
  useEffect(() => api.current?.scrollToOffset(0), [filter]);

  const text = file.data && chosen ? (drafts[chosen] ?? file.data.text) : null;
  const dirty = chosen !== null && file.data !== undefined && text !== file.data.text;
  const viewable = file.data !== undefined && !file.data.isBinary && !file.data.truncated;
  const writable = files.checkout !== null && viewable;

  function choose(path: string) {
    setChosen(path);
    setNote(null);
  }

  function saveChosen() {
    if (!chosen || !dirty || !writable || text === null || save.isPending) return;
    const path = chosen;
    save.mutate(
      { path, text },
      {
        onSuccess: () => {
          setDrafts((current) => {
            const rest = { ...current };
            delete rest[path];
            return rest;
          });
          setNote({ text: t("files.saved", { path }), failed: false });
        },
        onError: (error) => setNote({ text: t("files.saveFailed", { reason: explain(error) }), failed: true }),
      },
    );
  }

  function openChosen() {
    open.mutate(chosen, {
      onSuccess: (opened) => setNote({ text: t("files.opened", { editor: opened.editor }), failed: false }),
      onError: (error) => setNote({ text: t("files.openFailed", { reason: explain(error) }), failed: true }),
    });
  }

  // Enter opens a file or a directory, as Space does: react-arborist keeps Enter for renaming.
  function onTreeKey(event: KeyboardEvent) {
    const node = api.current?.focusedNode;
    if (event.key !== "Enter" || !node) return;
    event.preventDefault();
    if (node.isLeaf) {
      node.select();
      node.activate();
    } else node.toggle();
  }

  const language = chosen ? languageOf(chosen) : null;
  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        border: "1px solid var(--border)",
        borderRadius: "var(--radius-xl)",
        overflow: "hidden",
        minWidth: 0,
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "center",
          flexWrap: "wrap",
          gap: 12,
          padding: "10px 16px",
          borderBottom: "1px solid var(--border)",
          fontSize: "var(--text-xs)",
        }}
      >
        <span style={{ fontFamily: "var(--font-mono)", color: "var(--muted-foreground)" }}>
          {t("files.header", {
            branch: files.branch ?? t("files.detached"),
            count: String(files.modified.length),
          })}
        </span>
        {files.inWorktree ? (
          <span style={{ color: "var(--muted-foreground)" }}>{t("files.inWorktree")}</span>
        ) : null}
        {note ? (
          <span
            role={note.failed ? "alert" : "status"}
            style={{ color: note.failed ? "var(--destructive)" : "var(--muted-foreground)" }}
          >
            {note.text}
          </span>
        ) : null}
        <span style={{ marginLeft: "auto", display: "flex", gap: 8 }}>
          <Button variant="outline" size="sm" disabled={!files.checkout || open.isPending} onClick={openChosen}>
            {t("files.openInEditor")}
          </Button>
          <Button size="sm" disabled={!dirty || !writable || save.isPending} onClick={saveChosen}>
            {save.isPending ? t("files.saving") : t("files.save")}
          </Button>
        </span>
      </div>

      <div
        style={{
          display: "grid",
          gridTemplateColumns: narrow ? "minmax(0, 1fr)" : "280px minmax(0, 1fr)",
          gridTemplateRows: narrow ? "320px 480px" : "clamp(420px, 70vh, 820px)",
          minHeight: 0,
        }}
      >
        <div
          style={{
            display: "flex",
            flexDirection: "column",
            minHeight: 0,
            background: "var(--muted)",
            borderRight: narrow ? undefined : "1px solid var(--border)",
            borderBottom: narrow ? "1px solid var(--border)" : undefined,
          }}
        >
          <div style={{ display: "flex", padding: "10px 12px", borderBottom: "1px solid var(--border)" }}>
            <input
              type="search"
              className="pc-input"
              placeholder={t("files.filterPlaceholder")}
              aria-label={t("files.filter")}
              value={filter}
              onChange={(event) => setFilter(event.target.value)}
              style={{ flex: 1, height: "var(--control-h-sm)" }}
            />
          </div>
          <div ref={box} onKeyDown={onTreeKey} style={{ flex: 1, minHeight: 0 }}>
            {files.files.length === 0 ? (
              <p style={{ margin: 0, padding: 12, color: "var(--muted-foreground)" }}>{t("files.empty")}</p>
            ) : size.height > 0 ? (
              <Modified.Provider value={modified}>
                <Tree<FileEntry>
                  ref={api}
                  data={tree}
                  width={size.width}
                  height={size.height}
                  rowHeight={24}
                  indent={16}
                  padding={6}
                  openByDefault={false}
                  disableDrag
                  disableDrop
                  disableEdit
                  disableMultiSelection
                  selection={chosen ?? undefined}
                  searchTerm={filter.trim()}
                  searchMatch={(node, term) => node.data.name.toLowerCase().includes(term.toLowerCase())}
                  onActivate={(node) => (node.isLeaf ? choose(node.id) : node.toggle())}
                  aria-label={t("files.tree")}
                >
                  {Row}
                </Tree>
              </Modified.Provider>
            ) : null}
          </div>
          <p
            style={{
              margin: 0,
              padding: "8px 12px",
              borderTop: "1px solid var(--border)",
              fontSize: "var(--text-xs)",
              color: "var(--muted-foreground)",
            }}
          >
            {files.truncated
              ? t("files.truncated", { count: files.files.length.toLocaleString("en") })
              : t("files.count", { count: files.files.length.toLocaleString("en") })}
          </p>
        </div>

        <div style={{ display: "flex", flexDirection: "column", minHeight: 0, minWidth: 0 }}>
          {chosen ? (
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: 10,
                padding: "8px 16px",
                borderBottom: "1px solid var(--border)",
                fontFamily: "var(--font-mono)",
                fontSize: "var(--text-xs)",
                color: "var(--muted-foreground)",
                minWidth: 0,
              }}
            >
              <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{chosen}</span>
              {dirty ? <span style={{ color: "var(--chart-2)", flex: "none" }}>{t("files.notSaved")}</span> : null}
              {file.data && !writable ? <span style={{ flex: "none" }}>{t("files.readOnly")}</span> : null}
              {language ? <span style={{ marginLeft: "auto", flex: "none" }}>{language}</span> : null}
            </div>
          ) : null}
          <div style={{ flex: 1, minHeight: 0 }}>
            {!chosen ? (
              <p style={{ margin: 0, padding: 16, color: "var(--muted-foreground)" }}>{t("files.choose")}</p>
            ) : file.error ? (
              <ErrorState
                detail={t("files.fileFailed", { path: chosen, reason: explain(file.error) })}
                onRetry={() => void file.refetch()}
              />
            ) : !file.data ? (
              <Loading />
            ) : file.data.isBinary || file.data.truncated ? (
              <p style={{ margin: 0, padding: 16, color: "var(--muted-foreground)" }}>
                {t(file.data.isBinary ? "files.binary" : "files.tooLarge", {
                  path: chosen,
                  bytes: file.data.bytes.toLocaleString("en"),
                })}
              </p>
            ) : (
              <CodeEditor
                docKey={chosen}
                text={text ?? ""}
                path={chosen}
                readOnly={!writable}
                label={t("files.editorLabel", { path: chosen })}
                onChange={(next) => setDrafts((current) => ({ ...current, [chosen]: next }))}
                onSave={saveChosen}
              />
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

/** One row of the tree: a chevron for a directory, the name, and `M` on a modified file. */
function Row({ node, style }: NodeRendererProps<FileEntry>) {
  const modified = useContext(Modified);
  const changed = node.isLeaf && modified.has(node.id);
  return (
    <div
      style={{
        ...style,
        display: "flex",
        alignItems: "center",
        gap: 4,
        height: "100%",
        paddingRight: 8,
        fontFamily: "var(--font-mono)",
        fontSize: "var(--text-xs)",
        color: node.isLeaf ? "var(--foreground)" : "var(--muted-foreground)",
        background: node.isSelected ? "var(--accent)" : undefined,
        outline: node.isFocused ? "2px solid var(--ring)" : undefined,
        outlineOffset: -2,
        cursor: "pointer",
        whiteSpace: "nowrap",
      }}
    >
      {node.isLeaf ? (
        <span style={{ width: 12, flex: "none" }} />
      ) : node.isOpen ? (
        <ChevronDown size={12} aria-hidden style={{ flex: "none" }} />
      ) : (
        <ChevronRight size={12} aria-hidden style={{ flex: "none" }} />
      )}
      <span style={{ overflow: "hidden", textOverflow: "ellipsis" }}>{node.data.name}</span>
      {changed ? (
        <span title={t("files.modified")} style={{ color: "var(--chart-2)", flex: "none" }}>
          {t("files.modifiedMark")}
        </span>
      ) : null}
    </div>
  );
}

/** The size of an element, for react-arborist, which draws into a box of numbers. */
function useSize<T extends HTMLElement>(): [(node: T | null) => void, { width: number; height: number }] {
  const [host, setHost] = useState<T | null>(null);
  const [size, setSize] = useState({ width: 0, height: 0 });
  useEffect(() => {
    if (!host) return;
    const observer = new ResizeObserver(([entry]) => {
      if (entry) setSize({ width: Math.floor(entry.contentRect.width), height: Math.floor(entry.contentRect.height) });
    });
    observer.observe(host);
    return () => observer.disconnect();
  }, [host]);
  return [setHost, size];
}
