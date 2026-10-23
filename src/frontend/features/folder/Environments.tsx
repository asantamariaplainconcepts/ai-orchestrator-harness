import { useSetStateEnvironment, useStateEnvironments } from "@/shared/http/folder";
import { useMachine } from "@/shared/http/settings";
import { t } from "@/shared/i18n";
import { Select } from "@/shared/ui/kit";
import type { FolderBlockProps } from "./FolderBlockProps";
import { Failure, ListRow, Remedy, list, note, sectionTitle } from "./parts";

/**
 * Where each agent state runs (#612): one row per state of the loop that runs an agent, with the
 * environments this control plane has an adapter for. A change is stored at once and the next run of
 * that state is placed there. Scripts and scores always run in the default, because the gate needs
 * this machine.
 */
export function EnvironmentsBlock({ page, check }: FolderBlockProps) {
  const machine = useMachine();
  const chosen = useStateEnvironments(page.repository);
  const save = useSetStateEnvironment();
  const fallback = machine.data?.defaultEnvironment ?? "";
  const offered = machine.data?.environments ?? [];
  const agents = check?.data?.valid ? check.data.states.filter((state) => state.agent) : null;
  const stored = new Map((chosen.data?.states ?? []).map((row) => [row.state, row.environment]));
  const placed = agents !== null && agents.length > 0;
  const repository = page.repository;

  return (
    <section
      aria-label={t("folder.environments")}
      style={{ display: "flex", flexDirection: "column", gap: 8, minWidth: 0 }}
    >
      <h2 style={sectionTitle}>{t("folder.environments")}</h2>
      {repository === null ? (
        <span style={note}>{t("folder.environments.noRepository")}</span>
      ) : agents === null ? (
        <span style={note}>{t("folder.environments.noLoop")}</span>
      ) : agents.length === 0 ? (
        <span style={note}>{t("folder.environments.noAgent", { environment: fallback })}</span>
      ) : (
        <div style={list}>
          {agents.map((state, index) => {
            const pending = save.isPending && save.variables.state === state.name;
            const value = pending
              ? save.variables.environment
              : (stored.get(state.name) ?? fallback);
            return (
              <ListRow key={state.name} label={state.name} last={index === agents.length - 1} dense>
                <Select
                  mono
                  aria-label={t("folder.environments.select", { state: state.name })}
                  value={value}
                  disabled={pending}
                  onChange={(event) =>
                    save.mutate({ repository, state: state.name, environment: event.target.value })
                  }
                  style={{ height: 30, flex: "0 1 180px", minWidth: 0, fontSize: "var(--text-xs)" }}
                >
                  {offered.includes(value) ? null : (
                    <option value={value}>
                      {t("folder.environments.unknown", { environment: value })}
                    </option>
                  )}
                  {offered.map((name) => (
                    <option key={name} value={name}>
                      {name}
                    </option>
                  ))}
                </Select>
              </ListRow>
            );
          })}
        </div>
      )}
      <Failure error={chosen.error} />
      <Failure error={save.error} />
      {placed
        ? (machine.data?.notes ?? [])
            .filter((row) => [...stored.values()].includes(row.environment))
            .map((row) => (
              <Remedy
                key={row.environment}
                role="note"
                tone={row.offered ? "warning" : "failure"}
                headline={t(
                  row.offered ? "folder.environments.caution" : "folder.environments.unavailable",
                  { environment: row.environment },
                )}
                detail={row.note}
                remedy={row.remedy}
              />
            ))
        : null}
      {placed ? (
        <p style={note}>{t("folder.environments.note", { environment: fallback })}</p>
      ) : null}
    </section>
  );
}
