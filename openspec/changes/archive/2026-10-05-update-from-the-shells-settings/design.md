## Context

Measured on master c1656af6 and against Orbion's real implementation, read on this machine at
`platform-readonly/orbion`.

- **The shell has no message channel yet.** Photino gives the string pipe
  (`window.external.sendMessage` in, `SendWebMessage` out) and our `Program.cs` never routes it.
  Orbion's `WebMessages.cs` (id/method/body, camelCase, error-not-silence on unknown methods) is
  the shape that channel should have, and its `Program.cs` shows the single routing point.
- **The shell is already a Velopack install**: `pack.sh` builds it, `Packaged.IsInstalled` knows
  the installed state, and #442 puts full nupkgs + `releases.osx.json` in every release — the
  exact feed an `UpdateManager` reads.
- **The 2026-09-26 instalacion-velopack plan said "sin feed ni updater runtime hasta 1.0"** —
  that 1.0 is now; the epic is closed and the releases carry installers.
- **Orbion's model, worth copying whole** (`src/Orbion.Shell/Updates.cs`): nothing installs itself
  behind somebody's back — check quietly, ask once, restart; the question says what it costs;
  `status` is local-only; `check` reads the feed and downloads; `install` reuses the question;
  `ApplyUpdate`s stops the app's own API first (a version mismatch between window and server is
  the bug report nobody can act on); the credential is `GITHUB_TOKEN` then `gh auth token` —
  private repos 404 anonymously, which reads as "no releases" for ever; failures are sentences
  where the person pressed the button and silence where they did not.
- **Our differences**: our server is `AiOrchestrator.Server` started by `StartOwnedServer()`
  (Program.cs), stopped on window close today — `Updates` stops it by the API path `Program.cs`
  knows, matching on the executable's path.

## Goals / Non-Goals

**Goals:**

- The settings screen, in the installed shell: local status on open, Check (feed + quiet
  download), Install (one question, then restart into the new version).
- One general message channel — updates is the first method over it, not a second pipe.
- The dev shell (dotnet run / browser) sees an honest "not an install".

**Non-Goals:**

- Auto-update without the question — never while this product runs other people's work.
- A custom feed server — the vendor's releases page is the feed.
- Windows, or a visible download progress bar (Velopack downloads quietly; Install means
  seconds).
- Signed/notarized packages — its own change; Velopack's file swap does not need Gatekeeper's
  opinion on the update.

## Decisions

- **The channel is general from the first message.** `WebMessages` copied in shape (it is pure C#
  and its comments carry the reasoning), routing as one delegate in `Program.cs`; the next thing
  the page wants from its window is a method beside `updates`, not a new pipe.
- **Updates.cs follows Orbion's, adapted not rewritten**: feed = `asantamariaplainconcepts/ai-orchestrator-harness`;
  `Stop` matches the server executable path `Program.cs` started (not name-only, a dev checkout
  has its own `AiOrchestrator.Server`); everything else — the model, the question, the
  credential — is Orbion's reasoning, which was paid for.
- **`shared/shell.ts` is the one door** — a `call(method, body)` promise over the channel, with
  the no-shell answer built in (a browser tab has no `window.external.sendMessage`).
- **The question stays native** (Photino's `ShowMessage`), not a web dialog: it is the platform's
  own, it cannot be styled away by the page, and a webview asking permission to replace itself
  is asking a child whether its parent may leave.
- **No timers, anywhere.** Launch checks once; settings reads local facts on open. `staleTime:
  Infinity`, `refetchOnWindowFocus: false` — Orbion's exact numbers, for its exact reasons.

## Risks / Trade-offs

- **A page newer than its shell.** A fresh SPA served by an old server: unknown methods answer
  errors, the section draws the sentence. Acceptable — releases ship both halves in one package.
- **The private feed's credential.** Without `GITHUB_TOKEN`/`gh`, check fails with a sentence
  rather than a silent nothing. Same trade-off Orbion made, for the same reason.
- **The restart loses terminals mid-run.** The question says exactly that, Orbion's words —
  anything it runs stops with it — and a No keeps the package on disk for next time.

## Verification plan

1. In the installed app: Settings shows the installed version (local `status`).
2. With a release newer than the installed one in the repo: Check → available → Install → the
   native question → Yes restarts into the new version; No keeps it offered.
3. The old server is gone before the swap: after restart, `GET /api/version` answers the new
   number.
4. Dev shell (dotnet run) and plain browser: the section says "not an install".
5. No credential: Check shows the error sentence; nothing else happens.

## Questions to resolve before implementation

- None blocking: the Orbion source answers every shape question; this repo's differences are
  named above.
