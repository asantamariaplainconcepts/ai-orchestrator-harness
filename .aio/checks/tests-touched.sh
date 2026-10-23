#!/usr/bin/env bash
# Managed by aio. Install harness overwrites it.
# A finding when a test that existed at BASE was modified, deleted or renamed without being declared.
# Adding a test is ordinary; changing one that already stood is how a change gets a green gate
# by moving the goalposts. With no declaration, no existing test may change without a person.
. "$(dirname "$0")/_lib.sh"
change_init
declared=$(declared_list)

hits=0
while IFS=$'\t' read -r status old _new; do
  case $status in A*) continue ;; esac          # new files are the change's own
  is_test "$old" || continue
  if [ -n "$declared" ] && matches_any "$old" "$declared"; then continue; fi
  echo "existing test ${status:0:1}: $old"; hits=$((hits + 1))
done < <(change_status)

[ "$hits" -eq 0 ] || exit 1
