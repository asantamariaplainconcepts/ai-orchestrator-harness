#!/usr/bin/env bash
# Managed by aio. Install harness overwrites it.
# A finding when a method the change touches scores CRAP above the line: complex, and too little of
# it run by a test for anybody to change it with confidence. c² · (1 − cov)³ + c, 30 by default.
#
# Read from a crap.tsv: a `# commit<TAB><sha>` line naming the commit it was measured on, then one
# method per line, `crap<TAB>complexity<TAB>coverage<TAB>file<TAB>first<TAB>last<TAB>method`. In
# this repository `aspire do ci` writes it (CISteps/CoverageStep.cs). A report for another commit,
# or none, is "does not apply" rather than "could not tell": the gate runs before this in
# verifying, and a run without it should not be sent to a person for a number nobody measured.
# CHECK_CRAP overrides the path, CHECK_CRAP_MAX the line; .aio/steps/quality sets the
# line to 0 to hear of every touched method (#746).
. "$(dirname "$0")/_lib.sh"
change_init

report=${CHECK_CRAP:-src/artifacts/coverage/crap.tsv}
max=${CHECK_CRAP_MAX:-30}
[ -r "$report" ] || { echo "no $report: run the gate first"; exit 3; }
measured=$(sed -n 's/^# commit\t//p' "$report" | head -1)
[ "$measured" = "$HEAD_SHA" ] || { echo "$report was measured on ${measured:0:7}, not ${HEAD_SHA:0:7}"; exit 3; }

# The lines the change added or rewrote, as "path<TAB>first<TAB>last" hunks on the HEAD side.
hunks=$(mktemp); trap 'rm -f "$hunks"' EXIT
git diff -U0 "$BASE_SHA" "$HEAD_SHA" -- '*.cs' | awk '
  /^\+\+\+ / { path = substr($0, 7); next }
  /^@@ / {
    split($3, range, ","); start = substr(range[1], 2); count = (range[2] == "") ? 1 : range[2]
    if (count > 0) printf "%s\t%d\t%d\n", path, start, start + count - 1
  }' > "$hunks"
[ -s "$hunks" ] || exit 0

# A method is touched when any hunk overlaps its first..last line.
hits=$(awk -F'\t' -v max="$max" '
  NR == FNR { n++; path[n] = $1; from[n] = $2; to[n] = $3; next }
  /^#/ || $1 + 0 <= max + 0 { next }
  { for (i = 1; i <= n; i++) if (path[i] == $4 && from[i] <= $6 && to[i] >= $5) {
      printf "CRAP %s (complexity %s, coverage %s%%): %s:%s %s\n", $1, $2, $3, $4, $5, $7; break } }
' "$hunks" "$report")

[ -z "$hits" ] && exit 0
printf '%s\n' "$hits"
exit 1
