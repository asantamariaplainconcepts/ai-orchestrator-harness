#!/usr/bin/env bash
# Runs inside one probe sandbox and prints what an agent there can and cannot reach. probe.sh copies
# it in and runs it once per mount. Every line is a fact to read, not an assertion; nothing it prints
# is a secret — tokens are named by their first characters only, which for sbx are a placeholder.
#
#   inside.sh <workspace> <run-dir> <mount> <host-home>      mount: worktree | gitdir | clone
set -uo pipefail
WT=$1 RUN=$2 MOUNT=$3 HOST_HOME=$4
say() { printf '%-34s %s\n' "$1" "$2"; }
yes_no() { if "$@" >/dev/null 2>&1; then echo yes; else echo no; fi; }

echo "== inside ($MOUNT)"
say "kernel" "$(uname -r)"
say "user" "$(id -un), groups $(id -Gn | tr ' ' ,)"
say "claude" "$(claude --version 2>&1)"
say "permission mode in settings" "$(sed -n 's/.*"defaultMode": *"\([^"]*\)".*/\1/p' ~/.claude/settings.json)"
say "working directory" "$PWD"
say "workspace at the host's path" "$(yes_no test -d "$WT")"
say "can write the workspace" "$(yes_no test -w "$WT")"
say "host mounts" "$(awk '$2 ~ /^\/(Users|private|var\/folders)/ {split($4, o, ","); print $2" ("o[1]")"}' /proc/mounts | sort -u | tr '\n' ' ')"
say "the host's ~/.ssh visible" "$(yes_no test -d "$HOST_HOME/.ssh")"
say "the host's ~/.claude visible" "$(yes_no test -d "$HOST_HOME/.claude")"

echo "== git"
cd "$PWD" || exit 1
say "git rev-parse" "$(git rev-parse --is-inside-work-tree 2>&1 | head -1)"
if git rev-parse --git-dir >/dev/null 2>&1; then
  common=$(git rev-parse --git-common-dir)
  say "git common dir" "$common"
  say "branches visible" "$(git branch -a | wc -l | tr -d ' ')"
  say "current branch" "$(git branch --show-current)"
  say "worktrees visible" "$(git worktree list | wc -l | tr -d ' ')"
  say "other worktrees' metadata" "$(ls "$common/worktrees" 2>/dev/null | wc -l | tr -d ' ')"
  say "remotes" "$(git remote -v | awk '{print $1"="$2}' | sort -u | tr '\n' ' ')"
  echo "probe $(date -u +%FT%TZ)" > probe-613.txt
  git add probe-613.txt
  if git -c user.email=probe@example.invalid -c user.name=probe commit -qm "probe-613 from $MOUNT" 2>/tmp/commit.err; then
    say "commit" "yes, $(git rev-parse --short HEAD)"
  else
    say "commit" "no: $(head -1 /tmp/commit.err)"
  fi
  # A hook or core.hooksPath written here would run on the host the next time the host runs git.
  if touch "$common/hooks/probe-613" 2>/dev/null; then rm -f "$common/hooks/probe-613"; say "can write the repo's hooks" "yes"; else say "can write the repo's hooks" "no"; fi
  say "can write the repo's config" "$(yes_no test -w "$common/config")"
  say "can move another branch's ref" "$(yes_no test -w "$common/refs/heads")"
fi

echo "== end record"
if [ -d "$RUN" ]; then
  out=$(AIO_DONE_FILE="$RUN/done.json" node .aio/aio done approved --reason "probe-613 $MOUNT" 2>&1)
  say "aio done into the mounted run dir" "$out"
else
  say "aio done into the mounted run dir" "run dir not mounted"
fi
out=$(env -u AIO_DONE_FILE node .aio/aio done approved --reason "probe-613 $MOUNT, default path" 2>&1)
say "aio done, default path" "$out"

echo "== credentials"
say "Anthropic credential mode" "${SBX_CRED_ANTHROPIC_MODE:-unset}"
say "claude logged in" "$(claude auth status 2>&1 | sed -n 's/.*"loggedIn": *\([a-z]*\).*/\1/p')"
say "~/.claude/.credentials.json" "$(yes_no test -f ~/.claude/.credentials.json)"
say "GH_TOKEN" "${GH_TOKEN:+set, starts ${GH_TOKEN:0:12}}"
say "gh api user" "$(gh api user --jq .login 2>&1 | head -1)"
say "token scopes (via proxy)" "$(gh api -i user 2>/dev/null | tr -d '\r' | sed -n 's/^[Xx]-[Oo][Aa]uth-[Ss]copes: //p')"
say "git credential helper" "$(git config --get-all credential.helper | tr '\n' ' ')"
say "ssh agent identities" "$(ssh-add -l 2>&1 | head -1)"
if git rev-parse --git-dir >/dev/null 2>&1; then
  url=$(git remote get-url origin 2>/dev/null)
  if [ -n "$url" ]; then
    say "git ls-remote origin" "$(GIT_TERMINAL_PROMPT=0 timeout 30 git ls-remote "$url" HEAD 2>&1 | head -1 | cut -c1-60)"
    say "git push to origin" "$(GIT_TERMINAL_PROMPT=0 timeout 60 git push -q "$url" "HEAD:refs/heads/probe-613-$MOUNT" 2>&1 | head -1 | cut -c1-90)"
    say "pushed branch exists" "$(GIT_TERMINAL_PROMPT=0 timeout 30 git ls-remote "$url" "refs/heads/probe-613-$MOUNT" | wc -l | tr -d ' ')"
  fi
fi
