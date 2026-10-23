import { ChevronDown, ChevronRight, Ellipsis, Plus, SquareTerminal, Sparkles } from "lucide-react";
import { useState } from "react";
import { NavLink, useMatch } from "react-router";
import type { LiveSession, LoopFolder, VendorPbi } from "@/shared/http/api";
import { stalled } from "@/shared/http/api";
import { useFolders, usePbis, useSessions } from "@/shared/http/queries";
import { t } from "@/shared/i18n";
import { Badge, Button, MenuButton } from "@/shared/ui/kit";
import type { Theme } from "@/shared/ui/useTheme";

/** What the rail asks the layout to open: a dialog that a later issue replaces with the real flow. */
export type RailAsk =
  | { kind: "addFolder" }
  | { kind: "terminal"; folder: LoopFolder }
  | { kind: "explore"; folder: LoopFolder };

/**
 * The rail (DESIGN.md#the-rail, variant A of the canvas): PBIs with their count,
 * Loop with how many need you, the folders with their active PBIs, and a one-line footer.
 *
 * Everything it shows is read from `/api`. A PBI is active when it carries a state of the loop;
 * one with no state is not in the loop yet, and the PBIs screen lists it. A pause marks a hold and a
 * dot a live run, which is the tray's own list matched by repository and id.
 */
export function Rail({
  theme,
  onToggleTheme,
  onAsk,
}: {
  theme: Theme;
  onToggleTheme: () => void;
  onAsk: (ask: RailAsk) => void;
}) {
  const pbis = usePbis();
  const folders = useFolders();
  const sessions = useSessions();
  const selected = useMatch("/pbis/:folderId/:pbiId/*")?.params;

  const open = pbis.data?.pbis ?? [];
  const live = new Set((sessions.data ?? []).map(liveKey));
  // The Loop screen's Needs you band: a hold, or a loop run gone silent (#546).
  const silent = new Set((sessions.data ?? []).filter(stalled).map(liveKey));
  const held = open.filter(
    (pbi) => pbi.held || silent.has(liveKey({ repository: pbi.repository, pbi: pbi.id })),
  ).length;

  return (
    <nav
      aria-label={t("rail.label")}
      style={{
        width: 240,
        height: "100%",
        flex: "none",
        display: "flex",
        flexDirection: "column",
        background: "var(--sidebar)",
        color: "var(--sidebar-foreground)",
        borderRight: "1px solid var(--sidebar-border)",
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 10,
          padding: "18px 16px 14px",
          fontWeight: 600,
        }}
      >
        {t("app.name")}
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: 2, padding: "0 8px" }}>
        <RailLink to="/pbis" end={false} exceptPbi>
          <span>{t("rail.pbis")}</span>
          <span style={{ fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>
            {pbis.data ? open.length : null}
          </span>
        </RailLink>
        <RailLink to="/loop">
          <span>{t("rail.loop")}</span>
          {held > 0 ? (
            <span title={t("rail.needYouHint")}>
              <Badge variant="warning">{t("rail.needYou", { count: String(held) })}</Badge>
            </span>
          ) : null}
        </RailLink>
      </div>

      <div
        style={{
          flex: 1,
          minHeight: 0,
          overflowY: "auto",
          display: "flex",
          flexDirection: "column",
          gap: 2,
          padding: "20px 8px 8px",
        }}
      >
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            padding: "0 10px 6px",
            fontSize: "var(--text-xs)",
            color: "var(--muted-foreground)",
          }}
        >
          <span>
            {folders.data
              ? t("rail.folders", { count: String(folders.data.length) })
              : t("rail.foldersHeading")}
          </span>
          <Button
            variant="outline"
            size="icon-xs"
            aria-label={t("rail.addFolder")}
            title={t("rail.addFolder")}
            onClick={() => onAsk({ kind: "addFolder" })}
          >
            <Plus size={14} />
          </Button>
        </div>

        {folders.error ? (
          <RailNote>{t("rail.foldersFailed", { reason: folders.error.message })}</RailNote>
        ) : null}
        {pbis.error ? (
          <RailNote>{t("rail.pbisFailed", { reason: pbis.error.message })}</RailNote>
        ) : null}
        {folders.data?.length === 0 ? <RailNote>{t("rail.noFolders")}</RailNote> : null}

        {(folders.data ?? []).map((folder) => (
          <FolderGroup
            key={folder.id}
            folder={folder}
            pbis={open.filter((pbi) => pbi.folderPath === folder.path && pbi.state !== null)}
            unread={pbis.data?.unread.find((entry) => entry.folderPath === folder.path)?.why}
            live={live}
            selected={selected}
            onAsk={onAsk}
          />
        ))}
      </div>

      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          padding: "8px 12px",
          borderTop: "1px solid var(--sidebar-border)",
          fontSize: "var(--text-xs)",
        }}
      >
        <RailLink to="/settings" quiet>
          {t("rail.settings")}
        </RailLink>
        <Button
          variant="ghost"
          size="xs"
          onClick={onToggleTheme}
          aria-label={t("rail.theme")}
          title={t("rail.theme")}
          style={{ color: "var(--muted-foreground)" }}
        >
          {theme === "dark" ? t("rail.toLight") : t("rail.toDark")}
        </Button>
      </div>
    </nav>
  );
}

function liveKey(session: Pick<LiveSession, "repository" | "pbi">): string {
  return `${session.repository}#${session.pbi}`;
}

function FolderGroup({
  folder,
  pbis,
  unread,
  live,
  selected,
  onAsk,
}: {
  folder: LoopFolder;
  pbis: VendorPbi[];
  unread: string | undefined;
  live: Set<string>;
  selected: { folderId?: string; pbiId?: string } | undefined;
  onAsk: (ask: RailAsk) => void;
}) {
  const [expanded, setExpanded] = useState(true);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 2, paddingTop: 4 }}>
      <div style={{ display: "flex", alignItems: "center", gap: 2 }}>
        <button
          type="button"
          className="aio-rail-row"
          aria-expanded={expanded}
          aria-label={t("rail.toggleFolder", { name: folder.name })}
          title={t("rail.toggleFolder", { name: folder.name })}
          onClick={() => setExpanded((shown) => !shown)}
          style={{
            flex: "none",
            display: "flex",
            alignItems: "center",
            padding: "6px 2px 6px 10px",
            border: 0,
            borderRadius: "var(--radius-md)",
            background: "transparent",
            color: "var(--muted-foreground)",
            cursor: "pointer",
          }}
        >
          {expanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
        </button>
        {/* #565. The name opens the folder's page; the chevron beside it shows or hides its PBIs. */}
        <NavLink
          to={`/folders/${folder.id}`}
          className="aio-rail-row"
          title={t("rail.openFolder", { name: folder.name })}
          style={({ isActive }) => ({
            flex: 1,
            minWidth: 0,
            display: "flex",
            alignItems: "center",
            gap: 6,
            padding: "6px 6px",
            borderRadius: "var(--radius-md)",
            background: isActive ? "var(--sidebar-accent)" : "transparent",
            color: isActive ? "var(--sidebar-accent-foreground)" : "inherit",
            fontWeight: 500,
            textDecoration: "none",
          })}
        >
          <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
            {folder.name}
          </span>
          {expanded ? null : (
            <span
              style={{
                marginLeft: "auto",
                fontSize: "var(--text-xs)",
                color: "var(--muted-foreground)",
              }}
            >
              {pbis.length}
            </span>
          )}
        </NavLink>
        <MenuButton
          label={t("rail.folderMenu", { name: folder.name })}
          floating
          choices={[
            {
              key: "terminal",
              label: t("menu.terminal"),
              icon: <SquareTerminal size={14} />,
              onSelect: () => onAsk({ kind: "terminal", folder }),
            },
            {
              key: "explore",
              label: t("menu.explore"),
              icon: <Sparkles size={14} />,
              onSelect: () => onAsk({ kind: "explore", folder }),
            },
          ]}
        >
          <Ellipsis size={14} />
        </MenuButton>
      </div>

      {expanded ? (
        <>
          {unread ? (
            <RailNote>
              {t("rail.unread", { repository: folder.repository ?? folder.name, why: unread })}
            </RailNote>
          ) : null}
          {pbis.length === 0 && !unread ? <RailNote>{t("rail.noActive")}</RailNote> : null}
          {pbis.map((pbi) => (
            <PbiRow
              key={`${pbi.repository}#${pbi.id}`}
              folderId={folder.id}
              pbi={pbi}
              live={live.has(liveKey({ repository: pbi.repository, pbi: pbi.id }))}
              selected={selected?.folderId === folder.id && selected.pbiId === pbi.id}
            />
          ))}
        </>
      ) : null}
    </div>
  );
}

function PbiRow({
  folderId,
  pbi,
  live,
  selected,
}: {
  folderId: string;
  pbi: VendorPbi;
  live: boolean;
  selected: boolean;
}) {
  return (
    <NavLink
      to={`/pbis/${folderId}/${encodeURIComponent(pbi.id)}/journey`}
      className="aio-rail-row"
      aria-current={selected ? "page" : undefined}
      title={pbi.title}
      style={{
        display: "flex",
        alignItems: "center",
        gap: 8,
        padding: "5px 10px 5px 30px",
        borderRadius: "var(--radius-md)",
        background: selected ? "var(--sidebar-accent)" : "transparent",
        color: selected ? "var(--sidebar-accent-foreground)" : "inherit",
        fontSize: "var(--text-sm)",
        textDecoration: "none",
      }}
    >
      <span style={{ fontFamily: "var(--font-mono)", color: "var(--muted-foreground)" }}>
        #{pbi.id}
      </span>
      <span
        style={{ minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}
      >
        {pbi.title}
      </span>
      {pbi.held ? (
        <span
          role="img"
          aria-label={t("rail.held")}
          title={t("rail.held")}
          style={{ marginLeft: "auto", flex: "none", display: "flex", gap: 2 }}
        >
          <span style={pauseBar} />
          <span style={pauseBar} />
        </span>
      ) : null}
      {live ? (
        <span
          role="img"
          aria-label={t("rail.live")}
          title={t("rail.live")}
          style={{
            marginLeft: pbi.held ? 6 : "auto",
            flex: "none",
            width: 8,
            height: 8,
            borderRadius: "var(--radius-full)",
            background: "var(--primary)",
          }}
        />
      ) : null}
    </NavLink>
  );
}

const pauseBar: React.CSSProperties = {
  width: 3,
  height: 10,
  borderRadius: 1,
  background: "var(--pc-warning-700)",
};

/**
 * A rail destination. `exceptPbi` keeps PBIs from lighting up while a PBI is open: the PBI is
 * marked in its folder, and two marks would leave the eye to choose.
 */
function RailLink({
  to,
  end = true,
  exceptPbi = false,
  quiet = false,
  children,
}: {
  to: string;
  end?: boolean;
  exceptPbi?: boolean;
  quiet?: boolean;
  children: React.ReactNode;
}) {
  const onPbi = useMatch("/pbis/:folderId/:pbiId/*") !== null;

  return (
    <NavLink
      to={to}
      end={end}
      className="aio-rail-row"
      style={({ isActive }) => {
        const active = isActive && !(exceptPbi && onPbi);
        return quiet
          ? {
              padding: "4px 6px",
              borderRadius: "var(--radius-md)",
              color: active ? "var(--sidebar-primary)" : "var(--muted-foreground)",
              textDecoration: "none",
            }
          : {
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              padding: "8px 10px",
              borderRadius: "var(--radius-md)",
              background: active ? "var(--sidebar-accent)" : "transparent",
              color: active ? "var(--sidebar-accent-foreground)" : "inherit",
              fontWeight: 500,
              textDecoration: "none",
            };
      }}
    >
      {children}
    </NavLink>
  );
}

function RailNote({ children }: { children: React.ReactNode }) {
  return (
    <span
      style={{
        padding: "2px 10px 4px 30px",
        fontSize: "var(--text-xs)",
        color: "var(--muted-foreground)",
        overflowWrap: "anywhere",
      }}
    >
      {children}
    </span>
  );
}
