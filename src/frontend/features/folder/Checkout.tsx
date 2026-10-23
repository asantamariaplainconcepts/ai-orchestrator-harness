import { useState } from "react";
import { useNavigate } from "react-router";
import { useMachine, useRemoveFolder, useSetFolderDefaults } from "@/shared/http/settings";
import { t } from "@/shared/i18n";
import { Button, Dialog, Field, Select } from "@/shared/ui/kit";
import type { FolderBlockProps } from "./FolderBlockProps";
import { Failure, Labelled, sectionTitle } from "./parts";

/** Where the checkout is and what a session there starts with; and forgetting it. */
export function CheckoutBlock({ page }: FolderBlockProps) {
  const machine = useMachine();
  const save = useSetFolderDefaults();
  const remove = useRemoveFolder();
  const navigate = useNavigate();
  const [agent, setAgent] = useState(page.defaultAgent);
  const [model, setModel] = useState(page.defaultModel ?? "");
  const [removing, setRemoving] = useState(false);

  const changed = agent !== page.defaultAgent || model.trim() !== (page.defaultModel ?? "");
  const agents = machine.data?.agents ?? [];

  return (
    <section style={{ display: "flex", flexDirection: "column", gap: 8, minWidth: 0 }}>
      <h2 style={sectionTitle}>{t("folder.checkout")}</h2>
      <div
        style={{
          display: "flex",
          flexDirection: "column",
          gap: 8,
          padding: "12px 14px",
          border: "1px solid var(--border)",
          borderRadius: "var(--radius-xl)",
        }}
      >
        <Labelled inline label={t("folder.path")} mono>
          {page.path}
        </Labelled>
        <Labelled inline label={t("folder.branch")} mono={page.branch !== null}>
          {page.branch ?? t("folder.noBranch")}
        </Labelled>

        <form
          onSubmit={(event) => {
            event.preventDefault();
            if (changed)
              save.mutate({ id: page.id, agent, model: model.trim() === "" ? null : model.trim() });
          }}
          style={{
            display: "flex",
            alignItems: "flex-end",
            gap: 8,
            flexWrap: "wrap",
            marginTop: 4,
          }}
        >
          <div style={{ flex: "1 1 140px" }}>
            <Field label={t("settings.agent")}>
              <Select
                mono
                value={agent}
                onChange={(event) => setAgent(event.target.value)}
                style={{ height: 32 }}
              >
                {agents.includes(page.defaultAgent) ? null : (
                  <option value={page.defaultAgent}>
                    {t("settings.unknownAgent", { agent: page.defaultAgent })}
                  </option>
                )}
                {agents.map((name) => (
                  <option key={name} value={name}>
                    {name}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div style={{ flex: "1 1 160px" }}>
            <Field label={t("settings.model")}>
              <input
                className="pc-input"
                spellCheck={false}
                value={model}
                placeholder={t("settings.modelHint")}
                onChange={(event) => setModel(event.target.value)}
                style={{ height: 32, fontFamily: "var(--font-mono)" }}
              />
            </Field>
          </div>
          <Button size="sm" type="submit" disabled={!changed || save.isPending}>
            {t("settings.save")}
          </Button>
        </form>
        <Failure error={save.error} />

        <div>
          <Button
            variant="ghost"
            size="xs"
            onClick={() => setRemoving(true)}
            style={{ color: "var(--primary)" }}
          >
            {t("folder.remove")}
          </Button>
        </div>
      </div>

      {removing ? (
        <Dialog
          title={t("settings.removeTitle", { name: page.name })}
          onClose={() => setRemoving(false)}
          width={440}
          footer={
            <>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setRemoving(false)}
                style={{ marginLeft: "auto" }}
              >
                {t("settings.cancel")}
              </Button>
              <Button
                variant="destructive"
                size="sm"
                autoFocus
                disabled={remove.isPending}
                onClick={() => remove.mutate(page.id, { onSuccess: () => void navigate("/pbis") })}
              >
                {t("settings.remove")}
              </Button>
            </>
          }
        >
          <p style={{ margin: 0, color: "var(--muted-foreground)" }}>
            {t("settings.removeBody", { path: page.path })}
          </p>
          <Failure error={remove.error} />
        </Dialog>
      ) : null}
    </section>
  );
}
