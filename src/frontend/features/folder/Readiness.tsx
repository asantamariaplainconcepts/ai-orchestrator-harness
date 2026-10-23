import { explain } from "@/shared/http/client";
import type { FolderPage } from "@/shared/http/folder";
import { t } from "@/shared/i18n";
import { type Check, type FolderBlockProps, gateScript, loopFile } from "./FolderBlockProps";
import { Labelled, Remedy, note, warning } from "./parts";

type FolderTrust = FolderPage["trust"][number];

/**
 * The CLIs this folder's loop runs that ask to trust a folder first (#606): the default agent, and
 * each provider the loop names. The engine starts none of their runs here until the answer is yes.
 */
function asked(page: FolderPage, check: Check): FolderTrust[] {
  const providers = check?.data?.providers ?? [];
  return page.trust.filter(
    (row) => row.provider === page.defaultAgent || providers.includes(row.provider),
  );
}

/**
 * One cell per CLI asked: whether it trusts the folder, as the CLI keeps it. The console never
 * answers for the person: it shows the command that asks the question once, in their own terminal.
 */
export function TrustCells({ page, check }: FolderBlockProps) {
  return asked(page, check).map((row) => (
    <Labelled key={row.provider} label={t("folder.trust", { provider: row.provider })}>
      {row.trusted ? (
        <span>{t("folder.trust.yes")}</span>
      ) : (
        <Remedy
          tone="warning"
          headline={t("folder.trust.no")}
          detail={t("folder.trust.remedy")}
          remedy={row.remedy}
        />
      )}
    </Labelled>
  ));
}

/** What the folder lacks for the loop to walk it, in the order a person would fix it. */
function lacks(page: FolderPage, check: Check): string[] {
  const found: string[] = [];
  if (page.vendor === null) found.push(t("settings.lacksVendor"));
  if (page.loopYaml === null) found.push(t("settings.lacksLoop"));
  else if (check?.data && !check.data.valid)
    found.push(t("settings.lacksValid", { problems: check.data.problems.join("; ") }));
  else if (check?.error) found.push(t("settings.lacksValid", { problems: explain(check.error) }));
  const gate = page.harness.find((file) => file.path === gateScript);
  if (!gate?.present) found.push(t("settings.lacksGate"));
  else if (gate.stub) found.push(t("folder.lacksStub"));
  if (page.harnessVersion === null) found.push(t("folder.lacksHarness"));
  else {
    const missing = page.harness.filter(
      (file) => !file.present && file.path !== gateScript && file.path !== loopFile,
    ).length;
    if (missing === 1) found.push(t("folder.lacksFile"));
    else if (missing > 1) found.push(t("folder.lacksFiles", { count: String(missing) }));
  }
  for (const row of asked(page, check)) {
    if (!row.trusted) found.push(t("folder.lacksTrust", { provider: row.provider }));
  }
  return found;
}

export function Readiness({ page, check }: FolderBlockProps) {
  if (check?.isPending) return <span style={note}>{t("settings.checking")}</span>;
  const findings = lacks(page, check);
  if (findings.length === 0) return <span>{t("folder.ready")}</span>;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
      <span style={warning}>
        {findings.length === 1
          ? t("folder.missingOne")
          : t("folder.missingMany", { count: String(findings.length) })}
      </span>
      <ul style={{ margin: 0, paddingLeft: 18, display: "flex", flexDirection: "column", gap: 2 }}>
        {findings.map((finding) => (
          <li key={finding} style={{ fontSize: "var(--text-xs)" }}>
            {finding}
          </li>
        ))}
      </ul>
    </div>
  );
}
