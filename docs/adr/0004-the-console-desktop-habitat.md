# ADR-0004: The console's desktop habitat

- **Status:** Superseded by [ADR-0008](0008-the-installed-app-is-one-process.md) (2026-10-09)
- **Date:** 2026-09-04
- **Deciders:** repository owner
- **Tags:** frontend, infra, architecture

> **Accepted on 4 September 2026, and deliberately in two halves.** What is adopted **now** is
> Photino.NET, which is written, working and in the tree. What is recorded as the **direction** is
> Electron, for when the console has to be handed to somebody without the repository. Those are not in
> conflict: the first is a window, the second is a distribution, and the second is not started.
>
> **Accepted on partial evidence, and the gaps are named rather than smoothed over.** Photino was
> measured on macOS only. Tauri v2 was never built, by decision. The keyboard rows are unmeasured for
> the third time. Everything Electron is credited with here is read, not run.

## Context

The console opens by typing a 95-character command and pasting the port it prints into a browser
(`README.md:26`). `aspire start` holds a foreground process, so closing that terminal takes the app
with it.

[ADR-0001](0001-the-agent-terminal-runs-in-a-browser.md) chose the browser SPA over orca's Electron
architecture and rejected *moving to Electron* on the merits: it discards the .NET modular monolith
for one library, and gives up two properties orca then spends significant code re-acquiring — a run
outliving its viewer, and the app being reachable from another machine or a phone with no extra work.
It parked a **webview shell over the same server** separately and explicitly **not** on the merits:

> rejected *for now*, not on the merits: it fixes the keyboard capture and adds a dock presence
> without touching the backend, but it is strictly additive and can be adopted at any point. Taking it
> before the keyboard limitation has been felt would be paying for a fix to a problem whose size is
> unmeasured.

The measurement it named was issue #36, closed `not_planned` on 25 August 2026 without being taken.
Its bench still exists at [`keyboard-capture.html`](../keyboard-capture.html), and its one data point —
`Ctrl+C` failing to stop `aspire run` from the app's terminal — was recorded as a warning rather than
a finding, because the keys were injected by automation rather than typed.

**The motivation that reopened this is not the keyboard, and interrogating it removed most of it:**

| Originally wanted | Where it actually stands |
| --- | --- |
| A notification when a run needs a person | Owned by [#246](https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/246), which absorbed #249 on 26 August and kept its transport: blocking reasons interrupt "vía `IDesktopIntegration`" — from the server, through an existing seam. Not a habitat question. |
| The session coming back on relaunch | Habitat-independent. Which worktrees were open, which tab and the scrollback are `localStorage`; today only the theme and rail width persist (`shared/ui/useTheme.ts:18,37`, `app/AppShell.tsx:86,220`). Identical in a tab, an installed PWA and a shell. |
| Presence in the dock | Already available. `manifest.webmanifest` declares `display: standalone`, linked from `index.html:11`, and #36 verified the app installable. |

What survives is two things, and they are the two no browser habitat can give: a **global system
shortcut**, which a PWA cannot register by design, and **who starts the server**.

**The cost is no longer symmetric, which is why there are two candidates rather than one.** This
repository has no Rust — no `Cargo.toml`, no `rust-toolchain` anywhere — and the gate is `dotnet
restore` and `pnpm install`, then `dotnet build` and `pnpm build`, then `pnpm typecheck` and `pnpm
lint`, then `dotnet test`. **Tauri v2** is Rust and adds a third toolchain to a repository that has
two. **Photino.NET** is a NuGet package that lands in the solution that already exists — and is
deliberately thin, so it may not offer these capabilities at all, which moves the cost from a toolchain
to hand-written per-platform code rather than removing it. Which cost is smaller is the question, and
neither project's documentation answers it.

## Decision

**We will keep Photino.NET as the console's desktop shell, and treat Electron as the direction for
when distribution starts.**

**Why Photino now.** It is at `src/root/AiOrchestrator.Shell`, in the solution, built by the gate, and
it opens: verified through the accessibility tree at 255 elements with the real console inside it, not
a blank window. It costs nothing this repository was not already paying — no new toolchain, eleven
lines of csproj, 2.4 MB published against the operating system's own webview, clean through
`TreatWarningsAsErrors` and Roslynator. **And it keeps by construction the property ADR-0001 bought:**
it probes first and adopts a server that is already answering, and when it starts one it lets it go.
Closing the window closes a window — runs keep running and the console stays reachable through any
tunnel in front of loopback.

**What that does not buy, said plainly.** No global shortcut: 299 public members and no accelerator
API, so it would mean native code on three platforms. No tray. No webview pane inside the layout —
one `Load` per window. The window size request is not honoured. On macOS the icon needs the `.app`
bundle in `mac/bundle.sh`, because `SetIconFile` serves Windows and Linux and the dock reads
`CFBundleIconFile`.

**Why Electron is the direction and not the decision.** The moment this has to reach somebody without
the repository, the axis stops being price. Electron has `globalShortcut` stable — the one capability
this ADR attributes uniquely to a shell — plus `WebContentsView`, a tray, and `autoUpdater` with a
trodden signing path. Photino has none of the four and puts packaging outside its own scope. But
adopting it today would buy a window we already have, at the cost of a bundled Chromium and a major
every 8 weeks with only the latest three supported.

**The trigger is explicit, so nobody has to re-derive it.** Switch when the first of signing,
notarization, an installer or an updater becomes real work rather than a hypothetical. Replacing the
shell then costs about what writing it cost: eleven lines of csproj over a URL.

**And the honest sequencing, unchanged by this decision.** The shell is the smallest part of
distribution. Three things still have no answer: the .NET server has to be published as something
shippable rather than started through `aspire start`; ADR-0003 puts the SQLite file inside the
checkout, and there is no checkout on the recipient's machine; and the product shells out to `git` and
the agent CLIs, which the recipient must already have. None of them is in this ADR, and choosing any
shell answers none of them.

## The method

Three questions per candidate, answered **by running**, on **two operating systems** — macOS plus one
of Windows or Linux, with OS and version recorded, because presence, shortcuts and process supervision
differ per platform and a single-machine table reads as if it measured the decision.

Each answer is **three-valued, not boolean**: *built in*, *buildable — and at what cost*, or *no*.
"Photino does not ship a global shortcut" and "a global shortcut is not available" are different
answers with different costs, and collapsing them hides the one that matters.

### Question 1 — a global shortcut

Does a registered shortcut bring the console to the front **from another application**? Measured by
pressing it, not by reading that the API exists.

| | macOS | Windows or Linux |
| --- | --- | --- |
| Tauri v2 | *unmeasured — needs a Rust toolchain* | *unmeasured* |
| Photino.NET | **No, not built in.** | *unmeasured* |

**Photino, measured 4 September 2026 against 4.0.16, before it moved into `src/`.** `PhotinoWindow` exposes 299 public members
and **not one** matches `hotkey`, `shortcut`, `accelerator` or `key`. The only menu member is
`ContextMenuEnabled`, and there is no tray member at all. So the answer is not "the docs don't
mention it" — the API has no way to say it. A global shortcut on Photino means native code per
platform: `RegisterEventHotKey` or a global `NSEvent` monitor on macOS, `RegisterHotKey` on Windows,
X11 or a portal on Linux. That is where the cost moved when it left the toolchain.

Adjacent, and free: Photino **does** ship `SendNotification` / `NotificationsEnabled`. Out of scope
here — #246 owns notifications and put them on the server — but recorded because it is one of the
three motivations that walked away from this ADR, and it turns out this candidate would have covered
it.

### Question 2 — starting the server without owning it

**The constraint, stated before measuring, because getting it wrong is silent and expensive.** The
shell may *launch* the server; it may not *own* it. The server listens on loopback, and anything
tunnelling in front of it — Tailscale, for one — puts the console on a phone with no work from this
repository. A shell holding the server as its own child takes that away the moment it quits, and takes
every live run with it. That is exactly what ADR-0001 rejected Electron for:

> Electron kills its PTYs when the app quits, which is why orca carries scrollback snapshots,
> sleeping-agent records and per-vendor session resume — a substantial part of its complexity exists to
> survive its own process ending.

The bar is the rule this codebase already applies to everything it launches — `IDesktopIntegration`
spawns "each detached, so what it started outlives the request that asked for it".

**The phone test is the falsifier**, and a candidate that does not pass it has answered an easier
question:

1. Shell open — the console is visible from the phone over the usual tunnel.
2. **Shell closed — the console is still visible, and runs in flight are still running.**
3. Shell reopened — no second server starts, and it does not fight the one already running.

| | macOS | Windows or Linux |
| --- | --- | --- |
| Tauri v2 | *unmeasured — needs a Rust toolchain* | *unmeasured* |
| Photino.NET | **Yes**, on all three steps. | *unmeasured* |

**Photino, measured 4 September 2026 on macOS 25.6.** The bench spawns with the same call shape
`DesktopIntegration.Spawn` already uses — `Process.Start`, `UseShellExecute = false`, not redirected,
not waited on — and then exits.

1. **Survives.** Parent gone; child pid 46318 still alive with **`ppid` 1**, reparented to launchd
   rather than killed, and still answering `HTTP 200`.
2. **Adopts.** Started again against a live server, the bench prints `already running, adopting` and
   spawns nothing: one listener before, one listener after.
3. **Does not duplicate.** The probe is what makes the difference, not the shell.

**Two honest limits on that result.** The child is a plain HTTP listener, not `aspire start`: a second
AppHost on a machine already running one fights it for port 5080, and breaking the app to measure the
shell is a bad trade. What is being measured is the parent-child semantics, which the substitution
does not change. And this is a normal process exit rather than a Cmd-Q — the same for the child unless
the app is written to kill its children, which is exactly the behaviour ADR-0001 rejected Electron for
and which any adoption must be checked against.

**One trap worth writing down.** `RedirectStandardOutput = false` means the child *inherits* the
parent's stdout. Harmless for a GUI shell, which has no pipe — but during this bench it inherited a
harness pipe and the reader hung for two minutes after the parent had exited. A shell that spawns the
server must redirect the child's output to a file, or someone's terminal will hang on a process that
is doing nothing wrong.

### Question 3 — what it costs to keep

| | Fresh clone must install | Cold build | Binary size | In the gate? |
| --- | --- | --- | --- | --- |
| Tauri v2 | **A Rust toolchain** — there is none on this machine and none in the repository | *unmeasured* | *unmeasured* | No |
| Photino.NET | **Nothing new** — the .NET 10 SDK is already required | 1.17 s | 2.4 MB published | No |

**Photino, measured 4 September 2026.** `dotnet new console` plus one `PackageReference` to
Photino.NET 4.0.16. After `rm -rf bin obj`, a Release build takes **1.17 s** — with the NuGet package
already cached, so this is a cold build and not a cold restore. `dotnet publish` produces **2.4 MB**,
of which **2.2 MB is native runtimes for every platform at once** (win-x64 468K, osx-x64 448K,
osx-arm64 448K and the rest), framework-dependent against the SDK the repository already requires.

**Tauri's first cell is already answered and it did not need a prototype.** `which cargo rustc rustup`
finds nothing, and the repository has no `Cargo.toml` and no `rust-toolchain`. Whatever else Tauri
scores, its entry price on this machine is installing a toolchain that nothing else here uses.

### Alongside: the keyboard, with a real keyboard

`docs/keyboard-capture.html` run in each habitat — browser tab, installed PWA, and each shell — with
keys **typed**. Injected keys measure the injector, which is the trap #36 named and never escaped. This
closes #36 either with its measurement or with the written reason it no longer governs.

| Chords eaten | Tab | Installed PWA | Tauri v2 | Photino.NET |
| --- | --- | --- | --- | --- |
| macOS | | | | |
| Windows or Linux | | | | |

### Opened, at last — and it did not work the first time

Everything above was measured with the window switched off. Opening it on 4 September 2026 produced
two findings that no amount of building would have.

**It crashed on launch, and the cause is a rule rather than a bug.** `NSInternalInconsistencyException:
API misuse: setting the main menu on a non-main thread`. A single `await` in top-level statements makes
the entry point `async`, and the continuation resumes on a thread-pool thread; macOS requires the window
to be built on the **main** thread. The fix is that nothing above the window is awaited — the probe and
the wait are deliberately blocking. It compiled, passed the gate and crashed, which is the whole
argument for opening the thing.

**Then it worked, and the app inside it is the real one.** Verified through the accessibility tree
rather than by looking: **223 elements**, the projects rail with both checkouts and their worktrees,
the header, `⌘K`, and the worktree screen. Not a blank window and not an error page — a distinction
this repository has been burned by before, when `capture.sh` reported success while writing white PNGs.

**The window size is not honoured, and it fails differently depending on how you launch.**
`SetUseOsDefaultSize(false)` then `SetSize(1400, 900)` gave **2560×960** under `dotnet run` and
**178×136** — unusable — from the .app bundle. Two wrong answers from one call. Splitting it into
`SetWidth`/`SetHeight` did not fix it either; what makes it safe is `SetMinSize(1000, 700)`, which
cannot stop the window being too big but does stop it being too small to hold the console. Recorded
because it is the second promise of this API that did not survive being run.

**The icon needs a bundle on macOS, and `SetIconFile` is not enough.** The call is accepted and logged,
and the dock tile does not change: macOS reads `CFBundleIconFile` from the bundle and ignores what a
running process asks for. `SetIconFile` is what serves Windows and Linux. So `mac/bundle.sh` builds the
smallest `.app` that works — `dotnet publish` into `Contents/MacOS`, an `.icns` built by downscaling
the icon the frontend already ships, and an `Info.plist`. Verified: macOS then reports the process as
**"AI Orchestrator"** with `bundleId io.aiorchestrator.shell` instead of a nameless executable, the
`.icns` carries all ten variants up to 1024px, and the app renders the console inside it.

**That bundle is also the first inch of the distribution story**, and it is worth being clear about how
short an inch: no signing, no notarization, no installer, no updater. ADR-0004 recommends Electron
partly because those four are where the work actually is.

**And one harmless noise to expect:** `NSMapGet ... map table argument is NULL` on stdout at startup.
Cosmetic; the process is healthy after it.

## Consequences

- **Positive:** One thing to double-click, with its own window and dock icon, for a console whose daily
  loop is waiting. The two properties ADR-0001 bought stay bought — a run outlives its viewer, and the
  console is reachable from a phone through any tunnel — and the shell is written so it cannot take
  them away: it never supervises the server. The cost is a package reference and eleven lines of csproj.
- **Negative:** A second way to open the same product, which is a second thing to package, sign and keep
  in step. No global shortcut, and getting one means native code on three platforms. **`aspire` is
  looked up by name**, so a shell launched from the dock inherits the desktop session's `PATH` rather
  than the login shell's and may not find the CLI — `DesktopIntegration` already solves this with
  `LoginShellEnvironment.Path()`, and reaching it from a host would mean referencing Infrastructure, so
  it is an open question and not a copied helper. Launched from a terminal it works.
- **What it does not unlock, measured on the same API:** there is **no tab and no sub-webview**.
  `PhotinoWindow` has one `Load`, no `tab` member and no `child` member — only `Parent`, which is how a
  *second window* is opened. So one window is one full-size webview. Tabs drawn by the app in HTML keep
  working exactly as they do in the browser, because they are HTML; but a **webview pane beside the
  terminal** — the preview pane of idea 44 in #239 — is not reachable this way. It would need a second
  window, and a browser tab already does that for free. Worth knowing because it is the feature people
  assume a shell brings, and this one does not. (`Win32SetWebView2Path` confirms the rendering is the
  OS webview: WebView2, WKWebView, WebKitGTK.)
- **Neutral, and this is where the unmeasured rows come back:** ADR-0001's headline negative was that
  the browser eats `⌘W`, `⌘R` and `Ctrl+W` before the agent's TUI sees them. **A Photino window is a
  webview without browser chrome, so it may well eat fewer — and nobody has measured it.** That went
  from a nice-to-know to the most interesting open question the moment adopting became the
  recommendation, and #36 has now gone unmeasured three times. Tauri v2 is unmeasured **by choice**,
  not ruled out; if a global shortcut ever becomes the thing that matters, that is the comparison to
  take, and its first cost cell is already filled in. macOS is the only operating system measured. They live outside `src/`, never enter
  `AiOrchestrator.slnx`, and are **not added to `aspire do ci`** — a guard needed in opposite
  directions for each candidate, since Photino would join the solution frictionlessly and Tauri would
  put a toolchain into CI, and both failures look like progress. Once this ADR is written they are
  deleted or left unbuilt.

## Open questions

**Switch the shell now, or when distribution actually starts?** `AiOrchestrator.Shell` exists, is in
the solution, passes the gate and is eleven lines of csproj. Rewriting it against Electron now avoids
doing it twice; leaving it avoids doing it at all until the three prerequisites above are answered,
and it is the cheapest thing in this repository to throw away. This ADR does not decide it.

**Does Tauri v2 do the one thing Photino structurally cannot — a webview *inside* the layout?**
Unverified, and flagged as such because this ADR's own rule is that no cell is filled from
documentation. The recollection is that Tauri v2 added multiwebview — several webviews positioned
within one window rather than one per window — behind an `unstable` feature flag. If that holds it is
the **first capability where the two candidates differ in kind rather than in price**, and the
recommendation above rests on a comparison that did not know it: everything measured so far said
"Tauri probably matches Photino and charges a toolchain for it".

Two things keep this from being a reason to stop. It is only worth something if a webview pane is
wanted, and this repository has already decided twice that it is not — #239 discards the embedded
browser deliberately, and its idea 44 was scoped to the opposite: an "open preview" button over the
endpoint `aspire describe` already knows, a link rather than a pane. And adopting Photino does not
foreclose it, because the shell is eleven lines of csproj over a URL and replacing it later costs
about what writing it cost.

**What would close it:** build a prototype that puts a real webview pane beside a real terminal. Note
that Tauri is not the only way to ask: Electron hosting the URL has a stable API for several web
contents in one window, and would answer the same question without a Rust toolchain — for a bundled
browser engine instead, which is the trade to weigh rather than the toolchain. If panes ever become the
point, that is a two-candidate comparison of its own and it is not this ADR's.

**And the one still open from the method:** the keyboard rows. A Photino window is a webview without
browser chrome, so it may eat fewer chords than the browser does. That is ADR-0001's headline negative,
and it is the cheapest measurement left: `docs/keyboard-capture.html`, typed rather than injected.

## Alternatives considered

- **Move the backend to Node and adopt Electron, as orca did** — rejected on the merits by ADR-0001
  and not reopened here. None of the three gaps above is fixed by changing runtime.
- **Electron hosting only the URL** — rejected as a third candidate: it answers the same three
  questions as either candidate while charging an entire Node runtime, which ADR-0001 already weighed.
- **A phone client of its own (React Native)** — a relay problem, idea 28 of #239, and not on the table.
- **Split the API from the frontend so a shell can host the SPA itself** — rejected, and it is worth
  writing down why because it reads like the cheap option and is the expensive one. `grep` for
  `AddCors`, `UseCors` or `WithOrigins` across `src/root` and `src/shared` returns **nothing**:
  `ARCHITECTURE.md:113` states the property deliberately — "served same-origin by the host in every
  habitat — no API base URL, no CORS". Splitting means writing CORS from zero, for the SignalR hub as
  well as the API; it brings back an API base URL, which this repository has already been bitten by
  (#61: the literal `localhost:5080` in the Vite proxy sent requests to the *neighbouring worktree's*
  app); and it erodes the reasoning behind an identity composition whose default is one owner holding
  everything with no sign-in. **And it buys nothing a shell needs** — every candidate here loads the
  URL the server already serves.
- **Electron hosting only the URL** — *not* the Electron ADR-0001 rejected, and the distinction matters.
  That rejection was of Electron **as the architecture**: a Node main process owning the PTYs, which
  discards the .NET modular monolith. Electron as a window over `http://127.0.0.1:5080/` touches no
  backend code at all, exactly like the two candidates measured here. Not chosen, and **not measured** —
  but it is the candidate most likely to answer the pane question above, since several web contents in
  one window is a stable API there rather than an `unstable` flag. Recorded because an alternative
  missing from this list is one somebody re-proposes in three months.

  **Its price is not "a Node runtime", and saying so would be the same mistake as charging Photino for
  the .NET SDK.** `README.md:23` already requires "the .NET 10 SDK, Node 22+ with pnpm, and the Aspire
  CLI", and the gate runs four pnpm steps; Electron would arrive as a package through an install that
  already happens. The real price is at the other end: **Electron ships its own Chromium and its own
  Node inside the artifact**, where Photino borrows the operating system's webview — measured at
  **2.4 MB published, 2.2 MB of it native runtimes for every platform at once**. An Electron build is
  expected to be two orders of magnitude larger and brings a browser engine's update and security
  surface with it, which is a thing to own rather than a number to pay once. That expectation is
  **unmeasured**, like everything else recorded about Electron here.
- **The other JavaScript shells: NW.js, Neutralinojs, Electrobun** — looked at on 4 September 2026
  because "we already have Node" deserved to be tested against the whole field rather than against
  Electron alone. **That argument turns out to transfer only to Electron and NW.js**: Electrobun runs on
  **Bun**, a different runtime that adds roughly 60 MB to the bundle, and a Neutralino app's runtime is
  a C++ binary with Node appearing only in its CLI.

  - **NW.js** (41.2k stars, pushed 19 August 2026) bundles Chromium exactly as Electron does, so it
    saves nothing on size and brings a far thinner ecosystem for signing and updates. No reason to
    prefer it.
  - **Neutralinojs** (8.6k stars, v6.9.0, 180 open issues) is genuinely small and uses the OS webview,
    but its updater "only updates application resources": raising the Neutralino binary version means
    **the user reinstalls by hand**. For something whose point is staying patched, that is the wrong
    failure.
  - **Electrobun** (12.8k stars, MIT) is the one worth watching, and on the merits it beats Electron on
    both axes at once: the OS webview instead of a bundled Chromium, a `<electrobun-webview>` element
    that composites isolated webviews into the layout, and a BSDIFF updater producing **kilobyte-scale
    patches** rather than whole-app downloads, with installers and update artifacts generated for you.
    **Not chosen because of where it is, not what it is:** its current releases are `v2.0.2-beta.13`
    (30 August 2026) — still beta despite a "v1" announcement — with 104 open issues and documentation
    that lags the code. Signing and notarization is the part of a distribution story that can least
    afford to be a bet. **Revisit when it ships a stable line.**

- **A launcher script instead of a shell** — genuinely sufficient for startup, and this is the
  strongest argument against the recommendation: it obtains question 2's property with no dependency at
  all. Not chosen because it does not give a window, a dock icon or a chance at the keyboard, and
  Photino's price is low enough that the comparison is not really about cost.
- **Do nothing and keep the browser** — the status quo, and what a strict reading of the two questions
  would have concluded. Not chosen: at eleven lines and no new toolchain, the window does not need to
  win the argument the questions were framing.

## References

- [ADR-0001](0001-the-agent-terminal-runs-in-a-browser.md) — the decision this amends or supersedes
- [#266](https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/266) — the evaluation
- [#267](https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/267) — adopting a shell, blocked on this
- [#246](https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/246) — notifications, deliberately not decided here
- [#36](https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/36) — the keyboard measurement this closes
- [`keyboard-capture.html`](../keyboard-capture.html) — the bench
- `IDesktopIntegration` — the detached-spawn rule question 2 must satisfy
