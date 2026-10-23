import { explain } from "@/shared/http/client";
import {
  type PermissionMode,
  permissionModes,
  useRunsSettings,
  useSetPermissionMode,
} from "@/shared/http/settings";
import { t } from "@/shared/i18n";
import { Choices, ErrorState, Loading } from "@/shared/ui/kit";

/**
 * What the loop's agents may do without asking (#578): one mode for the instance, every run and every
 * session by hand, saved as soon as it is picked and read when the next one starts. Until somebody
 * picks one, it is `auto` (#706): in `allow-list` an unattended run stops at the first command outside the list.
 */
export function PermissionsSection() {
  const runs = useRunsSettings();
  const save = useSetPermissionMode();

  if (runs.error) return <ErrorState detail={explain(runs.error)} onRetry={() => void runs.refetch()} />;
  if (!runs.data) return <Loading />;

  const stored = runs.data.permissionMode;
  const value = (permissionModes as readonly string[]).includes(stored) ? (stored as PermissionMode) : "auto";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
      <Choices
        name="permission-mode"
        label={t("settings.permissions")}
        value={save.isPending && save.variables ? save.variables : value}
        busy={save.isPending}
        onChange={(mode) => save.mutate(mode)}
        options={[
          { value: "allow-list", title: "allow-list", detail: t("settings.permissions.allowList") },
          { value: "auto", title: "auto", detail: t("settings.permissions.auto") },
          { value: "yolo", title: "yolo", detail: t("settings.permissions.yolo") },
        ]}
      />
      <p style={{ margin: 0, fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>
        {t("settings.permissions.note")}
      </p>
      {save.error ? (
        <p role="alert" style={{ margin: 0, fontSize: "var(--text-xs)", color: "var(--destructive)" }}>
          {t("settings.permissions.failed", { reason: explain(save.error) })}
        </p>
      ) : null}
    </div>
  );
}
