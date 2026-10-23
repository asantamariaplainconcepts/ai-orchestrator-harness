import { useState } from "react";
import { explain } from "@/shared/http/client";
import type { JournalLine, RunRow, VendorPbi } from "@/shared/http/api";
import { usePbiAction, type LoopShape, type PbiAction } from "@/shared/http/queries";
import { t } from "@/shared/i18n";
import { Button, Dialog, Field, Select } from "@/shared/ui/kit";

/**
 * The header's one button (DESIGN.md, "The one button"). Its words depend on the state:
 *
 * | The PBI is                          | The button        |
 * | ----------------------------------- | ----------------- |
 * | in hold                             | Release hold      |
 * | with a live run of the loop         | Stop run          |
 * | closed in the vendor, or an epic    | nothing           |
 * | in a state the loop does not run    | Move to…          |
 * | anywhere else                       | nothing           |
 *
 * A closed PBI is reopened in the vendor, not moved from here, and an epic is closed by the engine
 * when its last sub-issue closes (#698), so neither is offered *Move to…*.
 *
 * A release whose hold leads somewhere goes there at once, as approving the pull request would; one
 * that leads nowhere asks where. Stopping a run asks first, because it cannot be taken back. A
 * refusal stays under the button in the server's words.
 */
export function HeaderAction({
  repository,
  id,
  pbi,
  epic,
  hold,
  live,
  loop,
  ready,
  size,
}: {
  repository: string;
  id: string;
  pbi: VendorPbi;
  /** Whether the PBI has sub-issues. */
  epic: boolean;
  /** The engine's hold line for the PBI's state, or null when a person put the hold on. */
  hold: JournalLine | null;
  live: RunRow | null;
  loop: LoopShape | undefined;
  /** Whether the runs, the journal, the loop and the sub-issues have been read, so the button is not a guess. */
  ready: boolean;
  size: "sm" | "md";
}) {
  const action = usePbiAction(repository, id);
  const [asking, setAsking] = useState<"release" | "stop" | "move" | null>(null);
  const [chosen, setChosen] = useState("");

  if (!ready || !loop) return null;

  const states = [...loop.states.map((state) => state.name), ...loop.undeclared];
  const declared = pbi.state !== null && loop.states.some((state) => state.name === pbi.state);

  const movable = !declared && !pbi.closed && !epic;
  const kind = pbi.held ? "release" : live ? "stop" : movable ? "move" : null;
  if (kind === null) return null;

  const run = (next: PbiAction) =>
    action.mutate(next, {
      onSuccess: () => {
        setAsking(null);
        setChosen("");
      },
    });

  const press = () => {
    action.reset();
    if (kind === "release" && hold?.to) run({ kind: "release" });
    else setAsking(kind);
  };

  const label =
    kind === "release" ? t("calls.pbi.release") : kind === "stop" ? t("calls.pbi.stop") : t("calls.pbi.move");

  const choices = states.filter((state) => state !== pbi.state);

  return (
    <>
      <Button
        variant={kind === "stop" ? "destructive" : "default"}
        size={size}
        onClick={press}
        disabled={action.isPending}
        title={
          kind === "release" && hold?.to ? t("calls.pbi.release.hint", { next: hold.to }) : undefined
        }
      >
        {label}
      </Button>

      {action.error && asking === null ? (
        <span role="alert" style={{ flexBasis: "100%", fontSize: "var(--text-xs)", color: "var(--destructive)" }}>
          {t("calls.pbi.refused", { reason: explain(action.error) })}
        </span>
      ) : null}

      {asking ? (
        <Dialog
          title={label}
          onClose={() => setAsking(null)}
          width={460}
          footer={
            <>
              {action.error ? (
                <span role="alert" style={{ fontSize: "var(--text-xs)", color: "var(--destructive)" }}>
                  {t("calls.pbi.refused", { reason: explain(action.error) })}
                </span>
              ) : null}
              <div style={{ flex: 1 }} />
              <Button variant="outline" size="sm" onClick={() => setAsking(null)}>
                {t("calls.pbi.cancel")}
              </Button>
              <Button
                variant={asking === "stop" ? "destructive" : "default"}
                size="sm"
                disabled={action.isPending || (asking !== "stop" && chosen === "")}
                onClick={() =>
                  asking === "stop" && live
                    ? run({ kind: "stop", runId: live.runId })
                    : asking === "release"
                      ? run({ kind: "release", to: chosen })
                      : run({ kind: "move", to: chosen })
                }
              >
                {label}
              </Button>
            </>
          }
        >
          <p style={{ margin: 0, color: "var(--muted-foreground)" }}>
            {asking === "stop"
              ? t("calls.pbi.stop.body", { command: live?.command ?? "", id })
              : asking === "release"
                ? t("calls.pbi.release.body", { id })
                : t("calls.pbi.move.body", { id, state: pbi.state ?? t("calls.pbi.noState") })}
          </p>
          {asking === "stop" ? null : (
            <Field label={t("calls.pbi.state")}>
              <Select mono value={chosen} onChange={(event) => setChosen(event.target.value)}>
                <option value="" disabled>
                  {t("calls.pbi.chooseState")}
                </option>
                {choices.map((state) => (
                  <option key={state} value={state}>
                    {state}
                  </option>
                ))}
              </Select>
            </Field>
          )}
        </Dialog>
      ) : null}
    </>
  );
}
