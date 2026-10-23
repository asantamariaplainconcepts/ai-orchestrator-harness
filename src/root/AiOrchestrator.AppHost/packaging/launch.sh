#!/bin/bash
# The one thing somebody who is not a developer launches.
#
# It stands where `aspire run` stands in the dev loop, and it has a harder job: a process started
# from Finder has no terminal to print to, no PATH worth having, and nobody watching it. Everything
# below follows from those three.
#
# Assembled into AiOrchestrator.app/Contents/MacOS/AiOrchestrator by package-macos.sh.
set -uo pipefail

BUNDLE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RESOURCES="$BUNDLE/Resources"
LOG_DIR="$HOME/Library/Logs/AiOrchestrator"
LOG="$LOG_DIR/launch.log"

PORT="${AIORCHESTRATOR_PORT:-5123}"
URL="http://127.0.0.1:$PORT"

mkdir -p "$LOG_DIR"

# Everything after this point is logged. A GUI process has no terminal, so without this the only
# evidence of a failed launch is a Dock icon that appears and vanishes.
exec >>"$LOG" 2>&1
echo "=== $(date -u +%Y-%m-%dT%H:%M:%SZ) launching ==="

# Errors reach the person, not the log they do not know about. `display dialog` because there is
# nothing else on a Mac that a double-clicked app can put in front of somebody.
say() {
  echo "FATAL: $1"
  /usr/bin/osascript -e "display dialog \"$1\" with title \"AI Orchestrator\" buttons {\"OK\"} default button 1 with icon caution" >/dev/null 2>&1
  exit 1
}

# ---------------------------------------------------------------------------------------------
# 1. The PATH. This is the whole reason A1 stopped being optional at this item.
#
# A GUI process inherits launchd's PATH — /usr/bin:/bin:/usr/sbin:/sbin and nothing else. Homebrew
# puts its binaries in /opt/homebrew/bin, and every agent CLI lives under a version manager in
# $HOME. None of them are visible here. The app then fails with "claude: command not found" while
# the identical command works in the user's terminal, which is the most expensive shape of bug
# there is.
#
# The login shell is asked rather than parsed, so anything a startup file computes rather than
# assigns is in the answer. LoginShellEnvironment.cs asks the same question from the server.
# ---------------------------------------------------------------------------------------------
LOGIN_PATH="$("${SHELL:-/bin/zsh}" -l -c 'printf %s "$PATH"' 2>/dev/null)"
if [ -n "$LOGIN_PATH" ]; then
  export PATH="$LOGIN_PATH:$PATH"
fi
echo "PATH=$PATH"

# ---------------------------------------------------------------------------------------------
# 2. The database, which is not this script's business any more.
#
# Seventy-nine lines stood here: a check that `docker` was on the PATH, `open -a Docker` and a
# minute of waiting, a generated password in a 0600 file, `docker volume create`, `docker run` with
# the Postgres 18 mount, and two readiness loops — one of them only because `docker run -d` exits 0
# once a container is *created*, which is not the same as running. ADR-0003 made every line of it
# unreachable: local persistence is one SQLite file, and a file has no server to start, no password
# to invent and no readiness to wait for.
#
# **And this script does not name the file either.** `LocalDatabase` in ServiceDefaults defaults it
# to ~/.aiorchestrator/aiorchestrator.db and creates that directory at 0700, so the Server and the
# MigrationService agree on the path by construction. A launcher that exported a connection string
# would be a second place the path is written, and the two places would drift the first time one of
# them changed — which is exactly what the AppHost's own comment says about deriving it per
# checkout. Setting ConnectionStrings__aiorchestratordb here still works, and is how you point an
# install at a database somewhere else.
# ---------------------------------------------------------------------------------------------

# Which database this launch used, said out loud. `open` hands the app the environment of whoever
# called it, so a person who has ever exported ConnectionStrings__aiorchestratordb in their shell —
# a developer, on this very machine — gets an installed app quietly attached to that database
# instead of its own. Explicit still wins, which is the rule; but the log is the only place a GUI
# process can say so, and without this line the evidence of which file was opened is an lsof away.
if [ -n "${ConnectionStrings__aiorchestratordb:-}" ]; then
  echo "database: inherited from the environment — $ConnectionStrings__aiorchestratordb"
else
  echo "database: the app's own default under ~/.aiorchestrator"
fi

# ---------------------------------------------------------------------------------------------
# 3. Migrations, as their own process and before the Server — the same order the AppHost uses, and
# for the same reason: a Server that starts against a schema-less database serves 500s that read
# like a broken product.
# ---------------------------------------------------------------------------------------------
echo "migrating"
"$RESOURCES/app/AiOrchestrator.MigrationService" || say "The database could not be brought up to date. See $LOG."

# ---------------------------------------------------------------------------------------------
# 4. The Server, in the foreground so that quitting the app stops it.
# ---------------------------------------------------------------------------------------------
export ASPNETCORE_URLS="$URL"
export ASPNETCORE_ENVIRONMENT=Production

# A process launched from Finder starts in `/`, and ASP.NET Core takes the content root from the
# current directory — so the SPA was looked for at /wwwroot and every page was a 404 while the API
# worked perfectly. The same shape as the PATH trap above and just as invisible from a terminal,
# where the working directory happens to be right.
#
# One directory for both binaries: they are the same framework and the same module assemblies, and
# publishing them separately put 146MB of duplicate .NET runtime in a 302MB bundle.
cd "$RESOURCES/app" || say "The application files are missing from the bundle."

echo "starting the server on $URL"
./AiOrchestrator.Server &
SERVER=$!

# Nothing outlives the app icon now. The database used to keep running between launches because
# starting it was the slow part; a file has no lifetime of its own, so the only process to stop is
# this one.
trap 'kill $SERVER 2>/dev/null' EXIT INT TERM

for _ in $(seq 1 60); do
  curl -fsS -o /dev/null "$URL/api/alive" && break
  kill -0 $SERVER 2>/dev/null || say "The server stopped while starting. See $LOG."
  sleep 1
done

if curl -fsS -o /dev/null "$URL/api/alive"; then
  echo "up; opening $URL"
  open "$URL"
else
  say "The server started but never answered on $URL. See $LOG."
fi

wait $SERVER
