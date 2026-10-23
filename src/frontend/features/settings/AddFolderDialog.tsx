import { ChevronRight, Folder, FolderGit2 } from "lucide-react";
import { Fragment, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router";
import { useQueryClient } from "@tanstack/react-query";
import { explain } from "@/shared/http/client";
import { settingsCalls, useAddFolder, useDirectories } from "@/shared/http/settings";
import { t } from "@/shared/i18n";
import { Badge, Button, Dialog, Field } from "@/shared/ui/kit";

/**
 * The rail's `+` (#515): a path on this machine becomes a folder, and its page (#565) opens with
 * what it lacks for the loop. A refusal stays in the dialog in the server's words, so the path can
 * be fixed where it was typed.
 *
 * #692. Below the field, a browser of this machine's directories from the server, because a browser
 * page cannot learn an absolute path. A click on a directory opens it; a click on a git repository
 * puts its path in the field. A path typed in the field that exists moves the browser there.
 */
export function AddFolderDialog({ onClose }: { onClose: () => void }) {
  const [path, setPath] = useState("");
  const [browsing, setBrowsing] = useState<string | null>(null);
  const add = useAddFolder();
  const navigate = useNavigate();
  const client = useQueryClient();
  const typing = useRef<number | undefined>(undefined);

  useEffect(() => () => window.clearTimeout(typing.current), []);

  const submit = () => {
    if (path.trim().length === 0 || add.isPending) return;
    add.mutate(path.trim(), {
      onSuccess: (added) => {
        onClose();
        void navigate(`/folders/${added.id}`);
      },
    });
  };

  // Asked once typing pauses; a path that is not there, or half typed, leaves the browser where it is.
  const follow = (typed: string) => {
    window.clearTimeout(typing.current);
    if (typed.length === 0) return;
    typing.current = window.setTimeout(() => {
      client
        .fetchQuery({
          queryKey: ["directories", typed],
          queryFn: () => settingsCalls.directories(typed),
        })
        .then((listing) => setBrowsing(listing.path))
        .catch(() => undefined);
    }, 300);
  };

  return (
    <Dialog
      title={t("rail.addFolder")}
      onClose={onClose}
      width={520}
      footer={
        <>
          <Button variant="outline" size="sm" onClick={onClose} style={{ marginLeft: "auto" }}>
            {t("settings.cancel")}
          </Button>
          <Button
            size="sm"
            type="submit"
            form="aio-add-folder"
            disabled={path.trim().length === 0 || add.isPending}
          >
            {add.isPending ? t("addFolder.adding") : t("rail.addFolder")}
          </Button>
        </>
      }
    >
      <form
        id="aio-add-folder"
        onSubmit={(event) => {
          event.preventDefault();
          submit();
        }}
        onKeyDown={(event) => {
          if (event.key === "Escape") onClose();
        }}
        style={{ display: "flex", flexDirection: "column", gap: 10 }}
      >
        <Field label={t("addFolder.path")} hint={t("addFolder.hint")}>
          <input
            className="pc-input"
            autoFocus
            spellCheck={false}
            value={path}
            placeholder={t("addFolder.placeholder")}
            onChange={(event) => {
              setPath(event.target.value);
              follow(event.target.value.trim());
            }}
            style={{ fontFamily: "var(--font-mono)" }}
          />
        </Field>
        <DirectoryBrowser at={browsing} chosen={path.trim()} onOpen={setBrowsing} onChoose={setPath} />
        {add.error ? (
          <p role="alert" style={{ margin: 0, color: "var(--destructive)", fontSize: "var(--text-xs)" }}>
            {explain(add.error)}
          </p>
        ) : null}
      </form>
    </Dialog>
  );
}

/** The path as buttons, one per directory from the root, then the directories inside it. */
function DirectoryBrowser({
  at,
  chosen,
  onOpen,
  onChoose,
}: {
  at: string | null;
  chosen: string;
  onOpen: (path: string) => void;
  onChoose: (path: string) => void;
}) {
  const listing = useDirectories(at);
  const segments = listing.data?.path.split("/").filter(Boolean) ?? [];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <nav
        aria-label={t("addFolder.breadcrumbs")}
        style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 2, fontFamily: "var(--font-mono)" }}
      >
        <Button variant="ghost" size="xs" aria-label={t("addFolder.root")} onClick={() => onOpen("/")}>
          /
        </Button>
        {segments.map((segment, index) => (
          <Fragment key={index}>
            {index > 0 ? (
              <ChevronRight size={12} aria-hidden style={{ color: "var(--muted-foreground)" }} />
            ) : null}
            <Button
              variant="ghost"
              size="xs"
              aria-current={index === segments.length - 1 ? "location" : undefined}
              onClick={() => onOpen(`/${segments.slice(0, index + 1).join("/")}`)}
            >
              {segment}
            </Button>
          </Fragment>
        ))}
      </nav>
      <div
        role="list"
        aria-label={t("addFolder.browser")}
        style={{
          display: "flex",
          flexDirection: "column",
          height: 220,
          overflow: "auto",
          padding: 4,
          border: "1px solid var(--border)",
          borderRadius: "var(--radius-md)",
          background: "var(--background)",
        }}
      >
        {listing.isPending ? (
          <Note>{t("state.loading")}</Note>
        ) : listing.isError ? (
          <Note alert>{explain(listing.error)}</Note>
        ) : listing.data.directories.length === 0 ? (
          <Note>{t("addFolder.empty")}</Note>
        ) : (
          listing.data.directories.map((directory) => (
            <div role="listitem" key={directory.path}>
              <Button
                variant={directory.isGitRepository && directory.path === chosen ? "secondary" : "ghost"}
                size="sm"
                aria-pressed={directory.isGitRepository ? directory.path === chosen : undefined}
                onClick={() =>
                  directory.isGitRepository ? onChoose(directory.path) : onOpen(directory.path)
                }
                style={{ width: "100%", justifyContent: "flex-start", gap: 8, fontFamily: "var(--font-mono)" }}
              >
                {directory.isGitRepository ? (
                  <FolderGit2 size={14} aria-hidden style={{ color: "var(--primary)" }} />
                ) : (
                  <Folder size={14} aria-hidden style={{ color: "var(--muted-foreground)" }} />
                )}
                <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                  {directory.name}
                </span>
                {directory.isGitRepository ? (
                  <Badge variant="info" style={{ marginLeft: "auto" }}>
                    {t("addFolder.repository")}
                  </Badge>
                ) : null}
              </Button>
            </div>
          ))
        )}
      </div>
    </div>
  );
}

function Note({ alert, children }: { alert?: boolean; children: string }) {
  return (
    <p
      role={alert ? "alert" : undefined}
      style={{
        margin: 8,
        fontSize: "var(--text-xs)",
        color: alert ? "var(--destructive)" : "var(--muted-foreground)",
      }}
    >
      {children}
    </p>
  );
}
