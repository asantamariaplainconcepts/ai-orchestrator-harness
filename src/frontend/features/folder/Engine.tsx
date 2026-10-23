import { explain } from "@/shared/http/client";
import { useLoopPresent, useSwitchEngine } from "@/shared/http/queries";
import { useEngineSettings } from "@/shared/http/settings";
import { t } from "@/shared/i18n";
import { ago } from "@/shared/ui/ago";
import { Switch } from "@/shared/ui/kit";
import type { FolderBlockProps } from "./FolderBlockProps";
import { Failure, failure, note } from "./parts";

/**
 * The switch the next scheduled pass follows (#562). Only a folder the engine walks has one: a
 * folder with no repository on GitHub, or a second clone of one already walked, says why not.
 */
export function Engine({ page, install }: FolderBlockProps) {
  const settings = useEngineSettings();
  const present = useLoopPresent();
  const flip = useSwitchEngine();

  if (settings.error) return <span style={failure}>{explain(settings.error)}</span>;
  if (!settings.data) return <span style={note}>{t("state.loading")}</span>;

  const walked = settings.data.folders.find((folder) => folder.path === page.path);
  if (!walked) {
    return (
      <span style={note}>
        {page.repository
          ? t("folder.engine.otherClone", { repository: page.repository })
          : t("folder.engine.noRepository")}
      </span>
    );
  }

  const on = flip.isPending ? flip.variables.on : walked.on;
  const last = present.data?.lastPass?.at;

  // #566. While the install pull request is open the engine is not offered: the checkout has no
  // harness to walk yet. An engine already on keeps its switch, so it can still be switched off.
  if (install?.pullRequest && !on) return <span style={note}>{t("folder.engine.afterMerge")}</span>;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
      <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
        {/* #564's switch: without a loop the server refuses it, so it is blocked and says why. */}
        <Switch
          on={on}
          label={t("folder.engine.switch", { name: page.name })}
          blocked={page.loopYaml === null}
          busy={flip.isPending}
          describedBy={page.loopYaml === null ? `engine-${page.id}` : undefined}
          onChange={(next) => flip.mutate({ repository: walked.repository, on: next })}
        />
        <span>
          {on
            ? t("folder.engine.on", { seconds: String(settings.data.intervalSeconds) })
            : t("folder.engine.off")}
          {on && last ? ` · ${t("folder.engine.last", { ago: ago(last) })}` : null}
        </span>
      </div>
      {page.loopYaml === null ? (
        <span id={`engine-${page.id}`} style={note}>
          {t("settings.lacksLoop")}
        </span>
      ) : null}
      <Failure error={flip.error} />
    </div>
  );
}
