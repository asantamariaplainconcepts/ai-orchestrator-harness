import { Sparkles, SquareTerminal } from "lucide-react";
import { useParams } from "react-router";
import { askSession } from "@/features/sessions/ask";
import { explain } from "@/shared/http/client";
import {
  type FolderPage,
  useFolderPage,
  useFolderStates,
  useHarnessInstall,
} from "@/shared/http/folder";
import { useFolders } from "@/shared/http/queries";
import { useLoopChecks } from "@/shared/http/settings";
import { t } from "@/shared/i18n";
import { Button, ErrorState, Loading } from "@/shared/ui/kit";
import { CheckoutBlock } from "./Checkout";
import { Engine } from "./Engine";
import { EnvironmentsBlock } from "./Environments";
import type { FolderBlockProps } from "./FolderBlockProps";
import { HarnessBlock, Version } from "./Harness";
import { LoopBlock } from "./Loop";
import { Readiness, TrustCells } from "./Readiness";
import { Labelled, note } from "./parts";

/**
 * The folder page (#565): how this checkout is set up for the loop, and what it lacks. A strip with
 * the engine's switch, the harness version against the one the app ships and what is missing; then
 * the loop as the file has it, validated again each time it changes on disk, the harness file by
 * file, and the checkout with its defaults. No PBI, run or file of the work: those have their
 * screens. Each block is its own file beside this one and takes the same `FolderBlockProps`.
 */
export function FolderScreen() {
  const { folderId = "" } = useParams();
  const page = useFolderPage(folderId);

  if (page.error && !page.data) {
    return <ErrorState detail={explain(page.error)} onRetry={() => void page.refetch()} />;
  }
  if (!page.data) return <Loading />;

  return <Folder page={page.data} />;
}

function Folder({ page }: { page: FolderPage }) {
  const checks = useLoopChecks(page.loopYaml ? [page.loopYaml] : []);
  const install = useHarnessInstall(page.id);
  const states = useFolderStates(page.id);
  const check = page.loopYaml ? checks[0] : undefined;
  const block: FolderBlockProps = { page, check, install: install.data, states };
  const folders = useFolders();
  const listed = folders.data?.find((folder) => folder.id === page.id);

  return (
    <div
      style={{
        padding: "24px clamp(16px, 4vw, 40px) 40px",
        display: "flex",
        flexDirection: "column",
        gap: 24,
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "flex-start",
          justifyContent: "space-between",
          gap: 16,
          flexWrap: "wrap",
        }}
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 6, minWidth: 0 }}>
          <h1
            style={{
              margin: 0,
              fontSize: "var(--text-lg)",
              fontWeight: 600,
              overflowWrap: "anywhere",
            }}
          >
            {page.name}
          </h1>
          <span style={{ ...note, overflowWrap: "anywhere" }}>
            {page.repository ? (
              <>
                <span style={{ fontFamily: "var(--font-mono)" }}>{page.repository}</span>
                {" · "}
                {page.vendor}
              </>
            ) : (
              t("folder.noRepository")
            )}
          </span>
        </div>
        {listed ? (
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            <Button
              variant="outline"
              size="sm"
              onClick={() => askSession({ kind: "terminal", folder: listed })}
            >
              <SquareTerminal size={14} />
              {t("menu.terminal")}
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => askSession({ kind: "explore", folder: listed })}
            >
              <Sparkles size={14} />
              {t("menu.explore")}
            </Button>
          </div>
        ) : null}
      </div>

      <section
        aria-label={t("folder.strip")}
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(min(100%, 240px), 1fr))",
          gap: 16,
          padding: "14px 16px",
          border: "1px solid var(--border)",
          borderRadius: "var(--radius-xl)",
          // Not --muted: the switch's off track is --input, which the dark theme makes that colour.
          background: "var(--card)",
          maxWidth: 1100,
        }}
      >
        <Labelled label={t("folder.engine")}>
          <Engine {...block} />
        </Labelled>
        <Labelled label={t("folder.harness")}>
          <Version {...block} />
        </Labelled>
        <Labelled label={t("folder.readiness")}>
          <Readiness {...block} />
        </Labelled>
        <TrustCells {...block} />
      </section>

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(min(100%, 420px), 1fr))",
          gap: 24,
          maxWidth: 1100,
          alignItems: "start",
        }}
      >
        <LoopBlock {...block} />
        <div style={{ display: "flex", flexDirection: "column", gap: 24, minWidth: 0 }}>
          <HarnessBlock {...block} />
          <EnvironmentsBlock {...block} />
          <CheckoutBlock {...block} />
        </div>
      </div>
    </div>
  );
}
