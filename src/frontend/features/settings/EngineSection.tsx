import { useState } from "react";
import { explain } from "@/shared/http/client";
import { useEngineSettings, useRunsSettings, useSaveEngineSettings } from "@/shared/http/settings";
import { t } from "@/shared/i18n";
import { Button, ErrorState, Field, Loading } from "@/shared/ui/kit";

/**
 * The control plane's own settings (#562): the seconds between two passes of the engine and how long
 * a live run may be silent before it reads as stale. Both are stored in the console's database and
 * read again on every pass, so a change needs no restart; until somebody saves one, it is the value
 * the Server's configuration gives.
 */
export function EngineSection() {
  const engine = useEngineSettings();
  const runs = useRunsSettings();

  if (engine.error || runs.error) {
    const failed = engine.error ?? runs.error;
    return (
      <ErrorState
        detail={explain(failed)}
        onRetry={() => {
          void engine.refetch();
          void runs.refetch();
        }}
      />
    );
  }
  if (!engine.data || !runs.data) return <Loading />;

  // Keyed by the stored values, so a save that comes back resets the form to what is stored.
  return (
    <EngineForm
      key={`${engine.data.intervalSeconds}:${runs.data.staleAfterSeconds}`}
      interval={engine.data.intervalSeconds}
      staleAfter={runs.data.staleAfterSeconds}
    />
  );
}

function EngineForm({ interval, staleAfter }: { interval: number; staleAfter: number }) {
  const save = useSaveEngineSettings();
  const [seconds, setSeconds] = useState(String(interval));
  const [stale, setStale] = useState(String(staleAfter));

  const intervalChanged = Number(seconds) !== interval;
  const staleChanged = Number(stale) !== staleAfter;
  const valid = Number.isInteger(Number(seconds)) && Number.isInteger(Number(stale)) && seconds !== "" && stale !== "";

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        save.mutate({
          interval: intervalChanged ? Number(seconds) : undefined,
          staleAfter: staleChanged ? Number(stale) : undefined,
        });
      }}
      style={{
        display: "flex",
        alignItems: "flex-end",
        gap: 8,
        flexWrap: "wrap",
        padding: "10px 14px",
        border: "1px solid var(--border)",
        borderRadius: "var(--radius-xl)",
      }}
    >
      <div style={{ flex: "1 1 160px" }}>
        <Field label={t("settings.engine.interval")}>
          <input
            className="pc-input"
            type="number"
            inputMode="numeric"
            min={10}
            max={86400}
            step={1}
            value={seconds}
            onChange={(event) => setSeconds(event.target.value)}
            style={{ height: 32, fontFamily: "var(--font-mono)" }}
          />
        </Field>
      </div>
      <div style={{ flex: "1 1 160px" }}>
        <Field label={t("settings.engine.stale")}>
          <input
            className="pc-input"
            type="number"
            inputMode="numeric"
            min={60}
            max={86400}
            step={1}
            value={stale}
            onChange={(event) => setStale(event.target.value)}
            style={{ height: 32, fontFamily: "var(--font-mono)" }}
          />
        </Field>
      </div>
      <Button size="sm" type="submit" disabled={!valid || (!intervalChanged && !staleChanged) || save.isPending}>
        {t("settings.save")}
      </Button>
      <p style={{ margin: 0, flexBasis: "100%", fontSize: "var(--text-xs)", color: "var(--muted-foreground)" }}>
        {t("settings.engine.note")}
      </p>
      {save.error ? (
        <p
          role="alert"
          style={{ margin: 0, flexBasis: "100%", fontSize: "var(--text-xs)", color: "var(--destructive)" }}
        >
          {explain(save.error)}
        </p>
      ) : null}
    </form>
  );
}
