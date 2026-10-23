#!/usr/bin/env bash
# Runs inside a probe sandbox: one HTTPS request per host, through the sandbox's proxy, and the
# answer. 000 means the connection was refused or timed out; 403 from the proxy means the policy
# blocked it (the proxy says so in the body). Hosts come as arguments, so probe.sh asks the same list
# before and after it changes the sandbox's rules.
set -uo pipefail
for host in "$@"; do
  body=$(curl -s -o /tmp/net.body -w '%{http_code}' --max-time 15 "https://$host/" 2>/dev/null)
  why=""
  grep -qiE 'blocked|denied|policy' /tmp/net.body 2>/dev/null && why="(policy)"
  printf '%-34s %s %s\n' "$host" "$body" "$why"
done
