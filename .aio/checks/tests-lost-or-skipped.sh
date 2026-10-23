#!/usr/bin/env bash
# Managed by aio. Install harness overwrites it.
# A finding when a test that existed at BASE is gone by name, or when the change adds a skip.
# Names, not counts: delete one and add another and the count stays where it was.
#
# Read statically from the source, so it needs no build and runs on any change. A controlled Run
# has the authoritative version — the manifest's `lost`, from the names the gate really ran — and
# this is the same question asked before anything is built. C# (xUnit [Fact]/[Theory]) is read;
# a modified test in any other language cannot be inventoried, and says so rather than passing.
. "$(dirname "$0")/_lib.sh"
change_init

inventory() { # "<rev>" "<path>": Class.Method for every [Fact]/[Theory] in that file at that rev
  # A test is named by the type whose braces enclose it. A type is only a name until it opens a
  # block, so a nested `record Detail(...);` names nothing: on aio-dogfood#65, taking the last type
  # declared renamed five untouched tests and reported them lost (#754).
  git show "$1:$2" 2>/dev/null | awk '
    /^[[:space:]]*(\/\/|\*)/ { next }
    /(^|[^A-Za-z0-9_])(class|record)[[:space:]]+[A-Za-z_]/ {
      line = $0; sub(/.*(class|record)[[:space:]]+/, "", line); sub(/[^A-Za-z0-9_].*/, "", line); declared = line }
    /\[(Fact|Theory)/ { pending = 1 }
    pending && /(void|Task)[^(]*\(/ && !/\[/ {
      line = $0; sub(/\(.*/, "", line); n = split(line, part, /[[:space:]]+/); print type[depth] "." part[n]; pending = 0 }
    {
      for (i = 1; i <= length($0); i++) {
        c = substr($0, i, 1)
        if (c == "{") { depth++; type[depth] = declared != "" ? declared : type[depth - 1]; declared = "" }
        else if (c == "}" && depth > 0) depth--
        else if (c == ";") declared = ""
      }
    }'
}

before=$(mktemp); after=$(mktemp); trap 'rm -f "$before" "$after"' EXIT
hits=0; unread=""
while IFS=$'\t' read -r status old new; do
  new=${new:-$old}
  is_test "$old" || is_test "$new" || continue
  case $status in A*) ;; *)
    case $old in *.cs) inventory "$BASE_SHA" "$old" >> "$before" ;;
      *) unread="$unread $old" ;; esac ;;
  esac
  case $status in D*) ;; *) case $new in *.cs) inventory "$HEAD_SHA" "$new" >> "$after" ;; esac ;; esac
  # A skip is whatever an added line of a test file says it is, in the spellings that exist.
  while IFS= read -r added; do
    echo "skip added in $new: $(printf '%s' "$added" | sed 's/^+[[:space:]]*//')"; hits=$((hits + 1))
  done < <(git diff -U0 "$BASE_SHA" "$HEAD_SHA" -- "$new" 2>/dev/null \
            | grep -E '^\+[^+]' | grep -E 'Skip[[:space:]]*=|\.(skip|only)\(|(^|[^A-Za-z])x(it|describe)\(|\[Ignore')
done < <(change_status)

# A test that moved between two changed files is in both sets and is not lost.
while IFS= read -r name; do
  echo "lost test: $name"; hits=$((hits + 1))
done < <(sort -u "$before" | comm -23 - <(sort -u "$after"))

[ "$hits" -eq 0 ] || exit 1
[ -z "$unread" ] || broken "only C# test names are read, and these changed:$unread"
