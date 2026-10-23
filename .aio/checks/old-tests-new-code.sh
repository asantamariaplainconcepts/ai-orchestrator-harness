#!/usr/bin/env bash
# Managed by aio. Install harness overwrites it.
# For every C# test file that existed at BASE and was modified, put its BASE version back over
# HEAD and run just those test classes — the old contract against the new code.
#   passes           clear: the edit was a refactor, what the test used to assert still holds
#   fails            a finding: the code changed behaviour and the test was edited to agree
#   does not build   could not tell: something the old test used is gone — a person reads that too
# It does not need to understand the edit, the same way a mutation does not read the code.
# It builds, so it takes minutes where the others take a second; a throwaway worktree at HEAD
# keeps the change's own checkout untouched.
. "$(dirname "$0")/_lib.sh"
change_init

files=$(change_status | awk -F'\t' '$1 ~ /^M/ && $2 ~ /\.cs$/ { print $2 }' | while IFS= read -r f; do
  is_test "$f" && echo "$f"; done)
if [ -z "$files" ]; then exit 0; fi
command -v dotnet >/dev/null || broken "dotnet is not on PATH"

# The real path, not mktemp's: on macOS /var is a symlink to /private/var, and a worktree reached
# through both spellings breaks MSBuild's project references — the build then fails on files the
# change never touched, which would read as the old tests not building when they do.
tmp=$(cd "$(mktemp -d)" && pwd -P); wt="$tmp/wt"
cleanup() { git worktree remove --force "$wt" >/dev/null 2>&1; rm -rf "$tmp"; }
trap cleanup EXIT
git worktree add --detach -q "$wt" "$HEAD_SHA" || broken "could not make a throwaway worktree at HEAD"

projects=""; classes=""
while IFS= read -r f; do
  git show "$BASE_SHA:$f" > "$wt/$f" || broken "could not restore $f from the base"
  dir=$(dirname "$wt/$f")
  while [ "$dir" != "$wt" ] && ! ls "$dir"/*.csproj >/dev/null 2>&1; do dir=$(dirname "$dir"); done
  [ "$dir" != "$wt" ] || broken "no test project above $f"
  projects="$projects$(ls "$dir"/*.csproj | head -1)"$'\n'
  classes="$classes$(awk '/(^|[^A-Za-z0-9_])class[[:space:]]+[A-Za-z_]/ { s = $0; sub(/.*class[[:space:]]+/, "", s); sub(/[^A-Za-z0-9_].*/, "", s); print s }' "$wt/$f")"$'\n'
done <<< "$files"

filter=$(printf '%s' "$classes" | sort -u | sed '/^$/d; s/.*/FullyQualifiedName~.&./' | paste -sd'|' -)
failed=0; unbuilt=""; empty=""
while IFS= read -r project; do
  [ -n "$project" ] || continue
  name=$(basename "$project" .csproj)
  if ! dotnet build "$project" -nologo -v q > "$tmp/build.log" 2>&1; then
    unbuilt="$unbuilt $name"; continue
  fi
  # The console logger at normal verbosity, because quiet prints neither the per-test lines nor
  # the total under VSTest 18 — and the total is what tells a pass from a filter that ran nothing.
  dotnet test "$project" --no-build -nologo --filter "$filter" \
    --logger "console;verbosity=normal" > "$tmp/test.log" 2>&1
  code=$?
  total=$(awk '/Total tests:/ { print $3 }' "$tmp/test.log" | tail -1)
  if [ -z "$total" ] || [ "$total" -eq 0 ]; then
    # Zero tests is not a pass — VerificationManifests says the same of a gate.
    empty="$empty $name"; continue
  fi
  if [ "$code" -ne 0 ]; then
    # One finding per test that no longer holds in its original form.
    grep -E '^[[:space:]]*Failed [A-Za-z]' "$tmp/test.log" | sed -E 's/^[[:space:]]*Failed /fails in its base form: /; s/ \[[^]]*\]$//' | head -10
    failed=$((failed + 1))
  fi
done < <(printf '%s' "$projects" | sort -u)

[ "$failed" -eq 0 ] || exit 1
[ -z "$empty" ] || broken "the restored tests ran nothing in$empty, so their old contract was never checked"
[ -z "$unbuilt" ] || broken "the old tests do not build against the new code in$unbuilt"
