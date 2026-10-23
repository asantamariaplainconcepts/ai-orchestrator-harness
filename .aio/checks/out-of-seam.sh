#!/usr/bin/env bash
# Managed by aio. Install harness overwrites it.
# A finding when the change touches a file its issue did not declare. The risk score was computed
# over the declared file-set, so a diff that leaves it is a different change from the one scored.
# Without a declaration the question does not arise: exit 3, and quality's row is not measured.
. "$(dirname "$0")/_lib.sh"
change_init
declared=$(declared_list)
if [ -z "$declared" ]; then echo "the issue declares no file-set (CHECK_DECLARED)"; exit 3; fi

hits=0
while IFS= read -r path; do
  if ! matches_any "$path" "$declared"; then echo "undeclared: $path"; hits=$((hits + 1)); fi
done < <(change_paths)

[ "$hits" -eq 0 ] || exit 1
