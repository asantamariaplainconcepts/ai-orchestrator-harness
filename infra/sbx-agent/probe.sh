#!/usr/bin/env bash
# The #613 probe: Claude Code inside a Docker sandbox (sbx), with a worktree of a throwaway
# repository mounted three ways. It measures what #614's adapter needs to know — how long a sandbox
# takes, whether git works, whether `aio done` lands where the host reads it, which credentials and
# hosts an agent in yolo can reach — and writes it to out/results.txt. Every line is a fact to read,
# not an assertion.
#
#   REPO=/path/to/throwaway/repo ./probe.sh          the whole probe, then removes what it made
#   REPO=/path/to/throwaway/repo ./probe.sh hello    only `claude -p "di hola"` in a fresh sandbox;
#                                                    it answers once a credential is stored, e.g.
#                                                    `sbx secret set anthropic --sandbox probe-613-hello`
#                                                    first (forgotten again on exit)
#   REPO=/path/to/throwaway/repo ./probe.sh credentials   only which secret the proxy injects
#   ./probe.sh down                                  removes every probe-613-* sandbox
#
# It pushes one branch per mount to REPO's origin and deletes them again from the host, so REPO must
# be a repository nobody minds. It never widens the global sbx policy: the network rules it adds are
# scoped to its own sandboxes and go with them.
set -uo pipefail
cd "$(dirname "$0")"
here=$PWD
SBX=${SBX:-sbx}
mkdir -p out
log() { printf '\n== %s\n' "$*" | tee -a out/results.txt; }
note() { printf '%s\n' "$*" | tee -a out/results.txt; }
ms() { python3 -c 'import time; print(int(time.time()*1000))'; }
free_kb() { df -k "$HOME" | awk 'NR==2 {print $4}'; }

down() {
  for name in $($SBX ls 2>/dev/null | awk 'NR>1 && $1 ~ /^probe-613-/ {print $1}'); do
    $SBX rm --force "$name" >/dev/null 2>&1 && note "removed sandbox $name"
  done
}

[ "${1:-}" = down ] && { down; exit 0; }
: "${REPO:?set REPO to a throwaway git repository}"
PROBE_DIR=${PROBE_DIR:-$(mktemp -d "${TMPDIR:-/tmp}/probe-613.XXXX")}
mkdir -p "$PROBE_DIR" && PROBE_DIR=$(cd "$PROBE_DIR" && pwd -P) || exit 1
WT=$PROBE_DIR/wt
RUN=$PROBE_DIR/run
branch=probe-613-$(date +%s)

setup() {
  git -C "$REPO" worktree add -q -b "$branch" "$WT" HEAD && mkdir -p "$RUN" || exit 1
}

teardown() {
  down
  forget_secrets
  for ref in $(git -C "$REPO" ls-remote origin 'refs/heads/probe-613-*' 2>/dev/null | awk '{print $2}'); do
    git -C "$REPO" push -q origin --delete "${ref#refs/heads/}" && note "deleted remote branch ${ref#refs/heads/}"
  done
  git -C "$REPO" worktree remove --force "$WT" 2>/dev/null
  git -C "$REPO" branch -D "$branch" >/dev/null 2>&1
  for remote in $(git -C "$REPO" remote | grep '^sandbox-probe-613-'); do git -C "$REPO" remote remove "$remote"; done
  rm -rf "$RUN"
}

hello() {
  local name=probe-613-hello
  $SBX create -q --name "$name" claude "$WT" >/dev/null
  log "claude -p \"di hola\" in $name"
  local t0; t0=$(ms)
  $SBX exec -w "$WT" "$name" claude -p "di hola" 2>&1 | tee -a out/results.txt
  note "answered in $(( $(ms) - t0 )) ms, sandbox start included"
}

# One sandbox per mount. Arguments after the mount name go to `sbx create`.
mount() {
  local mount=$1; shift
  local name=probe-613-$mount t0
  rm -f "$RUN/done.json" "$WT/.aio/done.json"
  log "$mount: sbx create $*"
  local free_before; free_before=$(free_kb)
  t0=$(ms)
  $SBX create -q --name "$name" "$@" >/dev/null 2>out/create-$mount.err || { note "create failed: $(tail -1 out/create-$mount.err)"; return; }
  note "created in $(( $(ms) - t0 )) ms"
  t0=$(ms)
  $SBX exec "$name" true >/dev/null 2>&1
  note "first exec (sandbox start) in $(( $(ms) - t0 )) ms"
  t0=$(ms)
  $SBX exec "$name" true >/dev/null 2>&1
  note "exec on a running sandbox in $(( $(ms) - t0 )) ms"

  $SBX cp "$here/inside.sh" "$name:/tmp/inside.sh"
  local workdir=(-w "$WT") workspace=$WT
  [ "$mount" = clone ] && workdir=() workspace=$REPO
  $SBX exec ${workdir[@]+"${workdir[@]}"} "$name" bash /tmp/inside.sh "$workspace" "$RUN" "$mount" "$HOME" 2>&1 | tee -a out/results.txt

  log "$mount: seen from the host"
  note "run dir done.json: $(cat "$RUN/done.json" 2>/dev/null || echo absent)"
  note "worktree .aio/done.json: $(cat "$WT/.aio/done.json" 2>/dev/null || echo absent)"
  note "worktree HEAD: $(git -C "$WT" log -1 --format='%h %s')"
  if [ "$mount" = clone ]; then
    note "host remotes: $(git -C "$REPO" remote | tr '\n' ' ')"
    local remote; remote=$(git -C "$REPO" remote | grep "^sandbox-$name" | head -1)
    if [ -n "$remote" ]; then
      git -C "$REPO" fetch -q "$remote" 2>&1 | tail -1
      note "fetched from $remote: $(git -C "$REPO" branch -r --list "$remote/*" --format='%(refname:short) %(subject)' | tr '\n' ';')"
    fi
  fi

  note "disk the sandbox took: $(( (free_before - $(free_kb)) / 1024 )) MB"
  $SBX stop "$name" >/dev/null 2>&1
  t0=$(ms)
  $SBX exec "$name" true >/dev/null 2>&1
  note "exec on a stopped sandbox in $(( $(ms) - t0 )) ms"
  # One at a time: each sandbox is a VM disk, and three at once filled this laptop's.
  $SBX rm --force "$name" >/dev/null 2>&1
}

network() {
  local name=probe-613-net-$(date +%s)
  $SBX create -q --name "$name" claude "$WT" >/dev/null || return
  local hosts=(api.anthropic.com statsig.anthropic.com github.com api.github.com codeload.github.com
    objects.githubusercontent.com api.nuget.org www.nuget.org builds.dotnet.microsoft.com
    registry.npmjs.org pypi.org example.com pastebin.com webhook.site gist.github.com
    raw.githubusercontent.com s3.amazonaws.com probe613.blob.core.windows.net)
  $SBX cp "$here/network.sh" "$name:/tmp/network.sh"
  log "network, global balanced policy plus the claude kit"
  $SBX exec "$name" bash /tmp/network.sh "${hosts[@]}" 2>&1 | tee -a out/results.txt

  log "network, after a per-sandbox deny of everything and an allow of api.anthropic.com"
  $SBX policy deny network --sandbox "$name" '**' >/dev/null
  $SBX policy allow network --sandbox "$name" 'api.anthropic.com' >/dev/null
  $SBX exec "$name" bash /tmp/network.sh api.anthropic.com github.com example.com 2>&1 | tee -a out/results.txt
  $SBX policy rm network --sandbox "$name" --resource '**' >/dev/null 2>&1
  $SBX policy rm network --sandbox "$name" --resource 'api.anthropic.com' >/dev/null 2>&1

  log "network, after a per-sandbox allow of what .NET restore needs"
  $SBX policy allow network --sandbox "$name" 'api.nuget.org,*.nuget.org,builds.dotnet.microsoft.com,dotnetcli.azureedge.net' >/dev/null
  $SBX exec "$name" bash /tmp/network.sh api.nuget.org www.nuget.org builds.dotnet.microsoft.com 2>&1 | tee -a out/results.txt

  log "network, per-sandbox deny of the exfiltration hosts balanced allows"
  $SBX policy deny network --sandbox "$name" 'gist.github.com,raw.githubusercontent.com,s3.amazonaws.com,*.blob.core.windows.net' >/dev/null
  $SBX exec "$name" bash /tmp/network.sh gist.github.com raw.githubusercontent.com s3.amazonaws.com probe613.blob.core.windows.net api.anthropic.com github.com 2>&1 | tee -a out/results.txt

  log "network, what the proxy logged for $name"
  $SBX policy log "$name" 2>&1 | tee -a out/results.txt
  $SBX rm --force "$name" >/dev/null 2>&1
}

# Which secret the proxy injects, shown with deliberately invalid values scoped to one sandbox: if the
# answer changes from the global secret's to a 401, the sandbox's own secret is the one that wins.
# No real token is created or read.
credentials() {
  local name=probe-613-cred
  local show='echo "anthropic mode: $SBX_CRED_ANTHROPIC_MODE"
    env | grep -i "^ANTHROPIC" | cut -c1-32
    echo "claude auth: $(claude auth status 2>&1 | tr -d " \n" | cut -c1-90)"
    echo "settings keys: $(python3 -c "import json,os;print(sorted(json.load(open(os.path.expanduser(\"~/.claude/settings.json\")))))")"
    echo "gh api user: $(gh api user --jq .login 2>&1 | tr -d "\n" | cut -c1-80)"
    echo "claude -p: $(timeout 60 claude -p "di hola" 2>&1 | tr "\n" " " | cut -c1-160)"'
  $SBX create -q --name "$name" claude "$WT" >/dev/null || return
  log "credentials, before any per-sandbox secret"
  $SBX exec -w "$WT" "$name" sh -c "$show" 2>&1 | tee -a out/results.txt
  echo probe-613-not-a-token | $SBX secret set github --sandbox "$name" 2>&1 | tail -1 | tee -a out/results.txt
  echo sk-ant-probe-613-not-a-key | $SBX secret set anthropic --sandbox "$name" 2>&1 | tail -1 | tee -a out/results.txt
  log "credentials, invalid github and anthropic secrets scoped to $name, set after create"
  $SBX exec -w "$WT" "$name" sh -c "$show" 2>&1 | tee -a out/results.txt
  # The same secrets, already stored when the sandbox is created again under the same name.
  $SBX rm --force "$name" >/dev/null 2>&1
  note "secrets still stored for $name after sbx rm: $($SBX secret ls 2>&1 | grep -c "^$name ")"
  $SBX create -q --name "$name" claude "$WT" >/dev/null || return
  log "credentials, the same secrets present before create"
  $SBX exec -w "$WT" "$name" sh -c "$show" 2>&1 | tee -a out/results.txt
  forget_secrets
}

forget_secrets() {
  for name in probe-613-cred probe-613-hello; do
    for service in github anthropic; do $SBX secret rm "$service" --sandbox "$name" -f >/dev/null 2>&1; done
  done
}

: > out/results.txt
log "sbx $($SBX version 2>&1 | awk '{print $3}'), repo $REPO, worktree $WT"
trap teardown EXIT
setup
case "${1:-all}" in
  hello) hello; exit 0 ;;
  credentials) credentials; exit 0 ;;
esac
mount worktree claude "$WT" "$RUN"
mount gitdir claude "$WT" "$RUN" "$REPO/.git"
mount clone --clone claude "$REPO" "$RUN"
network
credentials
hello
log "done — results in infra/sbx-agent/out/results.txt"
