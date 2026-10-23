import { explain } from "@/shared/http/client";
import { type FolderHarnessFile, useInstallHarness } from "@/shared/http/folder";
import { t } from "@/shared/i18n";
import { Badge, Button } from "@/shared/ui/kit";
import { type Check, type FolderBlockProps, gateScript, loopFile } from "./FolderBlockProps";
import { Failure, ListRow, list, note, sectionTitle, warning } from "./parts";

/**
 * The harness version the checkout has against the one this build carries, and the way to install
 * it (#566): *Install harness* when there is none, *Reinstall* when the app ships another version,
 * and the pull request instead of either while it is open. Both open a pull request from
 * `harness/install`; nothing is written to the default branch or to this checkout, which shows the
 * new version once the person merges and pulls.
 */
export function Version({ page, install }: FolderBlockProps) {
  const run = useInstallHarness(page.id);
  const installed = page.harnessVersion;
  const current = installed === page.shippedVersion;
  const pending = install?.pullRequest ?? null;
  const problems = run.data?.problems ?? [];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6, alignItems: "flex-start" }}>
      <span>
        {installed === null
          ? t("folder.version.none", { shipped: page.shippedVersion })
          : current
            ? t("folder.version.current", { version: installed })
            : t("folder.version.behind", { version: installed, shipped: page.shippedVersion })}
      </span>
      {pending ? (
        <span>
          {t("folder.install.proposed", { version: page.shippedVersion })}{" "}
          <a href={pending.url} target="_blank" rel="noreferrer">
            {t("folder.install.pr", { id: pending.id })}
          </a>
          {t("folder.install.notMerged")}
        </span>
      ) : current ? null : install === undefined ? (
        <span style={note}>{t("folder.install.asking")}</span>
      ) : (
        <>
          <Button
            variant={installed === null ? "default" : "outline"}
            size="xs"
            disabled={page.repository === null || run.isPending}
            aria-describedby={`install-${page.id}`}
            onClick={() => run.mutate()}
          >
            {run.isPending
              ? t("folder.install.busy")
              : installed === null
                ? t("folder.install", { version: page.shippedVersion })
                : t("folder.reinstall", { version: page.shippedVersion })}
          </Button>
          <span id={`install-${page.id}`} style={note}>
            {page.repository === null
              ? t("folder.install.noRepository")
              : t("folder.install.note", { repository: page.repository, branch: install.branch })}
          </span>
        </>
      )}
      {install?.problem ? <span style={note}>{install.problem}</span> : null}
      <Failure error={run.error} />
      {problems.length > 0 ? (
        <ul role="alert" style={{ ...warning, margin: 0, paddingLeft: 18 }}>
          {problems.map((problem) => (
            <li key={problem}>{problem}</li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}

/** The badge each status wears: a stub is as much a finding as a missing file. */
const tone = { ok: "success", stub: "warning", missing: "warning", unknown: "secondary" } as const;

type Row = { key: string; label: string; detail: string; status: keyof typeof tone };

/**
 * The package file by file, grouped by directory so ten scripts are one row, with the gate and the
 * loop on rows of their own because each is the repository's to make its own; then the states its
 * repository already has at the vendor.
 */
function harnessRows({ page, check, states }: FolderBlockProps): Row[] {
  const rows: Row[] = [];
  const groups = new Map<string, FolderHarnessFile[]>();
  for (const file of page.harness) {
    if (file.path === loopFile || file.path === gateScript) continue;
    const slash = file.path.lastIndexOf("/");
    const directory = slash < 0 ? file.path : file.path.slice(0, slash + 1);
    groups.set(directory, [...(groups.get(directory) ?? []), file]);
  }

  for (const path of [loopFile, gateScript]) {
    const file = page.harness.find((entry) => entry.path === path);
    if (file) rows.push({ key: path, label: path, ...own(file, check) });
  }

  for (const [directory, files] of groups) {
    const one = files.length === 1 ? files[0] : null;
    const missing = files
      .filter((file) => !file.present)
      .map((file) => file.path.slice(directory.length) || file.path);
    rows.push({
      key: directory,
      label: one ? one.path : directory,
      detail:
        missing.length > 0
          ? one
            ? t("folder.file.absent")
            : t("folder.file.missing", { names: missing.join(", ") })
          : one
            ? t("folder.file.present")
            : t("folder.file.all", { count: String(files.length) }),
      status: missing.length > 0 ? "missing" : "ok",
    });
  }

  const declared = check?.data?.valid ? check.data.states.map((state) => state.name) : [];
  const had = states.data?.states;
  const absent = had ? declared.filter((name) => !had.includes(name)) : [];
  rows.push({
    key: "states",
    label: t("folder.states"),
    detail: statesDetail(states, had, absent),
    status: !states.data || had == null ? "unknown" : absent.length > 0 ? "missing" : "ok",
  });

  return rows;
}

/** The loop's row carries the verdict; the gate's, whether it is still the package's stub. */
function own(file: FolderHarnessFile, check: Check): Pick<Row, "detail" | "status"> {
  if (!file.present) return { detail: t("folder.file.absent"), status: "missing" };
  if (file.path === gateScript) {
    return file.stub
      ? { detail: t("folder.file.stub"), status: "stub" }
      : { detail: t("folder.file.own"), status: "ok" };
  }
  if (!check?.data) return { detail: t("settings.checking"), status: "ok" };
  if (!check.data.valid) return { detail: t("folder.loopInvalid"), status: "stub" };
  const count = String(check.data.states.length);
  return { detail: t("folder.file.loopValid", { count }), status: "ok" };
}

function statesDetail(
  states: FolderBlockProps["states"],
  had: string[] | null | undefined,
  absent: string[],
): string {
  if (states.error) return explain(states.error);
  if (!states.data) return t("folder.states.asking");
  if (had == null) return states.data.problem ?? "";
  const vendor = states.data.vendor ?? "";
  if (absent.length > 0) return t("folder.states.missing", { vendor, names: absent.join(", ") });
  return t("folder.states.all", { count: String(had.length), vendor });
}

export function HarnessBlock(props: FolderBlockProps) {
  const rows = harnessRows(props);

  return (
    <section style={{ display: "flex", flexDirection: "column", gap: 8, minWidth: 0 }}>
      <h2 style={sectionTitle}>{t("folder.harnessBlock")}</h2>
      <div style={list}>
        {rows.map((row, index) => (
          <ListRow key={row.key} label={row.label} last={index === rows.length - 1}>
            <span style={{ ...note, flex: "1 1 160px", minWidth: 0, overflowWrap: "anywhere" }}>
              {row.detail}
            </span>
            <span style={{ marginLeft: "auto", flex: "none" }}>
              <Badge variant={tone[row.status]}>{t(`folder.status.${row.status}`)}</Badge>
            </span>
          </ListRow>
        ))}
      </div>
    </section>
  );
}
