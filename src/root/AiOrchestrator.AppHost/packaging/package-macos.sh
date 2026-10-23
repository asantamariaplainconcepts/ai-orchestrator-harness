#!/bin/bash
# Builds AiOrchestrator.app — the whole product as one thing to double-click.
#
# WHY NOT `aspire publish`. It works, and the Compose file it writes is wrong for this product.
# Tried, read, discarded: it containerises the Server, and the Server's entire job is to reach the
# host — it shells out to the user's `git` (GitCli), spawns their agent CLI in a pseudo-terminal
# (PtyAgentRunner), opens their editor and Finder (DesktopIntegration), and reads their keychain
# through `security` (KeychainSecretResolver). A containerised Server can do none of those, and it
# would fail as "agent not found" rather than as "this is the wrong topology".
#
# So: everything native on the host, because everything is about the host. This paragraph used to
# end "Postgres in a container, because a database in a container is exactly right" — ADR-0003
# retired the exception, and the app now starts with no container at all. That is the same
# conclusion orca reaches by being a desktop app in the first place — it drives your terminals and
# your worktrees, so it lives where they do.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../../.." && pwd)"
OUT="${1:-$REPO/artifacts}"
APP="$OUT/AiOrchestrator.app"

case "$(uname -m)" in
  arm64) RID=osx-arm64 ;;
  x86_64) RID=osx-x64 ;;
  *) echo "Unsupported architecture: $(uname -m)" >&2; exit 1 ;;
esac

echo "==> the SPA"
# Before the Server is published, not after: the bundle is copied into the Server's wwwroot, and
# publishing first would package the previous build — a stale UI being served by a fresh binary,
# which looks like a caching bug for as long as it takes to notice.
( cd "$REPO/src/frontend" && pnpm install --frozen-lockfile && pnpm build )

echo "==> the server and the migrator ($RID)"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# Self-contained on purpose. "Somebody who just wants to use this" does not have the .NET SDK, and
# telling them to install one is the developer's loop wearing a different hat.
#
# **One output directory for both.** They were published to Resources/server and
# Resources/migrations, and self-contained means each copy carried its own .NET runtime and its own
# four module assemblies: 156MB and 146MB inside one 302MB bundle. Publishing both here is safe
# rather than lucky — every version comes from src/Directory.Packages.props, so the shared framework
# files are byte-identical, and the per-app files (.deps.json, .runtimeconfig.json and the native
# apphost) are named after their own assembly. PublishSingleFile stays false, or two single-file
# bundles would have nothing to share.
for project in AiOrchestrator.Server AiOrchestrator.MigrationService; do
  dotnet publish "$REPO/src/root/$project" \
    --configuration Release \
    --runtime "$RID" \
    --self-contained true \
    --output "$APP/Contents/Resources/app" \
    -p:PublishSingleFile=false
done

echo "==> the bundle"
install -m 755 "$HERE/launch.sh" "$APP/Contents/MacOS/AiOrchestrator"
sed "s/__VERSION__/$(git -C "$REPO" describe --tags --always 2>/dev/null || echo 0.0.0)/" \
  "$HERE/Info.plist" > "$APP/Contents/Info.plist"

# Ad-hoc signed. Not for distribution — for the keychain: an unsigned bundle gets a new code
# identity every build, and macOS treats each one as a different application asking for the same
# item. Signing it makes that identity stable, which is what H1's `security` reads depend on
# staying silent. Real distribution needs a Developer ID and notarisation; that is a release
# pipeline's job, not this script's, and it is named here so nobody assumes this is it (#35).
codesign --force --deep --sign - "$APP" >/dev/null 2>&1 || echo "warning: could not sign the bundle"

echo
echo "Built $APP"
echo "Try it:  open '$APP'"
echo "Log:     ~/Library/Logs/AiOrchestrator/launch.log"
