import { useOpenInEditor } from "@/shared/http/settings";
import { t } from "@/shared/i18n";
import { Badge, Button } from "@/shared/ui/kit";
import { type FolderBlockProps, loopFile } from "./FolderBlockProps";
import { Failure, failure, note, sectionTitle } from "./parts";

/**
 * The loop as the file has it, read-only, with its verdict and the way to change it. The console
 * never writes the loop: *Open in editor* hands the file to the person's editor, and the page reads
 * it again on its next poll.
 */
export function LoopBlock({ page, check }: FolderBlockProps) {
  const open = useOpenInEditor();
  const states = check?.data?.valid ? check.data.states.length : 0;

  return (
    <section style={{ display: "flex", flexDirection: "column", gap: 8, minWidth: 0 }}>
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 12,
          flexWrap: "wrap",
        }}
      >
        <h2 style={sectionTitle}>{t("folder.loop")}</h2>
        {page.loopYaml !== null ? (
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            {check?.data ? (
              check.data.valid ? (
                <Badge variant="success">{t("folder.loopValid", { count: String(states) })}</Badge>
              ) : (
                <Badge variant="error">{t("folder.loopInvalid")}</Badge>
              )
            ) : null}
            <Button
              variant="outline"
              size="sm"
              disabled={open.isPending}
              onClick={() => open.mutate({ id: page.id, file: loopFile })}
            >
              {t("settings.openInEditor")}
            </Button>
          </div>
        ) : null}
      </div>
      {open.data ? (
        <span style={note}>{t("settings.opened", { editor: open.data.editor })}</span>
      ) : null}
      <Failure error={open.error} />
      {page.loopYaml === null ? (
        <div className="pc-empty">
          <span className="pc-empty__desc">{t("settings.lacksLoop")}</span>
        </div>
      ) : (
        <>
          {check?.data && !check.data.valid ? (
            <ul aria-label={t("folder.loopProblems")} style={{ ...failure, paddingLeft: 18 }}>
              {check.data.problems.map((problem) => (
                <li key={problem}>{problem}</li>
              ))}
            </ul>
          ) : null}
          <pre
            tabIndex={0}
            aria-label={loopFile}
            style={{
              margin: 0,
              padding: "14px 16px",
              border: "1px solid var(--border)",
              borderRadius: "var(--radius-xl)",
              background: "var(--muted)",
              color: "var(--foreground)",
              fontFamily: "var(--font-mono)",
              fontSize: "var(--text-xs)",
              lineHeight: 1.6,
              overflowX: "auto",
              whiteSpace: "pre",
            }}
          >
            {page.loopYaml}
          </pre>
        </>
      )}
      <p style={note}>{t("folder.loopNote")}</p>
    </section>
  );
}
