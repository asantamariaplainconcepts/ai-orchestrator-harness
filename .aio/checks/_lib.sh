# Managed by aio. Install harness overwrites it.
# Sourced by the checks, never run: the leading underscore is what keeps a runner from calling it
# one. A check answers with its exit code: 0 clear, 1 a finding per line, 2 could not tell, 3 does
# not apply, each with one line saying why. The package's quality runs every one of them and makes
# each a row of its comment (#746, #758).
#
# A change is BASE..HEAD of a clean tree. BASE is the merge-base with the trunk; CHECK_BASE
# overrides it, and quality always sets it to the pull request's base.

set -uo pipefail
export LC_ALL=C  # git's sentences are read here, and the C locale keeps them in English (#762)

broken() { echo "$*"; exit 2; }

change_init() {
  git rev-parse --git-dir >/dev/null 2>&1 || broken "not a git checkout"
  # A dirty tree is refused rather than read: the answer has to describe a commit, or "clear"
  # would be a statement about files nobody committed.
  [ -z "$(git status --porcelain)" ] || broken "the tree is not clean, so no commit is being checked"
  HEAD_SHA=$(git rev-parse HEAD 2>/dev/null) || broken "no HEAD"
  BASE_SHA=${CHECK_BASE:-$(git merge-base HEAD "${CHECK_TRUNK:-origin/master}" 2>/dev/null)}
  [ -n "$BASE_SHA" ] || broken "no trunk to compare against"
  BASE_SHA=$(git rev-parse --verify -q "$BASE_SHA^{commit}") || broken "the base is not a commit here"
}

# "STATUS<TAB>path" or, for a rename or copy, "STATUS<TAB>old<TAB>new".
change_status() { git diff --name-status -M "$BASE_SHA" "$HEAD_SHA"; }

# Every path the change names, both sides of a rename: moving a file out of a place counts as
# touching that place.
change_paths() { change_status | cut -f2- | tr '\t' '\n' | sort -u; }

# What a test file is, by its name: a __tests__ folder, .test. or .spec., test_ or test-, or a
# name ending in Test or Tests. In this repository it mirrors TestClaims.IsTest.
is_test() {
  local path=$1 name lowered stem
  case "/$(printf '%s' "$path" | tr '[:upper:]' '[:lower:]')/" in */__tests__/*) return 0 ;; esac
  name=${path##*/}
  lowered=$(printf '%s' "$name" | tr '[:upper:]' '[:lower:]')
  case $lowered in *.test.* | *.spec.* | test_* | test-*) return 0 ;; esac
  stem=${name%.*}
  case $(printf '%s' "$stem" | tr '[:upper:]' '[:lower:]') in *_test | *_tests | *-test | *-tests) return 0 ;; esac
  # The capital T carries its own boundary — FooTests.cs is a test, contest.cs is not.
  case $stem in *Test | *Tests) return 0 ;; esac
  return 1
}

# Whether a path matches any line of a pattern list: one pattern per line, # comments, and a
# `*` that crosses directories, because `case` globbing does.
matches_any() {
  local path=$1 list=$2 pattern
  while IFS= read -r pattern || [ -n "$pattern" ]; do
    pattern=${pattern%%#*}
    pattern=$(printf '%s' "$pattern" | sed 's/[[:space:]]*$//')
    [ -n "$pattern" ] || continue
    # shellcheck disable=SC2254 — the pattern is meant to glob
    case $path in $pattern) return 0 ;; esac
  done < "$list"
  return 1
}

# The file-set the issue declared, one path or pattern per line, in the file CHECK_DECLARED names.
# quality reads it out of the issue's Dónde cae and hands it in; run by hand, nothing is declared.
declared_list() {
  if [ -n "${CHECK_DECLARED:-}" ]; then
    [ -r "$CHECK_DECLARED" ] || broken "CHECK_DECLARED names $CHECK_DECLARED, which cannot be read"
    printf '%s' "$CHECK_DECLARED"
  fi
}
