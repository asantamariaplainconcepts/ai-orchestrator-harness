import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { explain } from "@/shared/http/client";
import { startAtLogin } from "@/shared/http/api";
import { t } from "@/shared/i18n";
import { Switch } from "@/shared/ui/kit";

/**
 * *Start at login* (#703, ADR-0010): the same as `aio service install|uninstall`, for the installed
 * tool only. Started with `dnx` or from a checkout there is no tool command to register, so the
 * switch is blocked and the line under it says why; on Windows there is no service yet.
 */
export function StartAtLoginSection() {
  const client = useQueryClient();
  const state = useQuery({ queryKey: ["start-at-login"], queryFn: startAtLogin.read, retry: false });
  const flip = useMutation({
    mutationFn: startAtLogin.set,
    onSuccess: (answer) => client.setQueryData(["start-at-login"], answer),
  });

  if (!state.data) return null;

  const { how, supported, registered } = state.data;
  const why =
    how === "dnx"
      ? t("settings.login.dnx")
      : how !== "tool"
        ? t("settings.login.checkout")
        : !supported
          ? t("settings.login.windows")
          : null;
  const on = flip.isPending ? flip.variables : registered;

  return (
    <section style={{ display: "flex", flexDirection: "column", gap: 4 }}>
      <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
        <Switch
          on={on}
          label={t("settings.login.switch")}
          blocked={why !== null}
          busy={flip.isPending}
          describedBy="start-at-login"
          onChange={(next) => flip.mutate(next)}
        />
        <span style={{ fontSize: "var(--text-sm)", fontWeight: 500 }}>{t("settings.login.switch")}</span>
      </div>
      <div id="start-at-login" style={muted}>
        {why ?? (on ? t("settings.login.on") : t("settings.login.off"))}
      </div>
      {flip.error ? (
        <div role="alert" style={{ ...muted, color: "var(--destructive)" }}>
          {explain(flip.error)}
        </div>
      ) : null}
    </section>
  );
}

const muted: React.CSSProperties = { fontSize: "var(--text-xs)", color: "var(--muted-foreground)" };
