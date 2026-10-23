import { WebLinksAddon } from "@xterm/addon-web-links";
import type { IDisposable, Terminal } from "@xterm/xterm";

/**
 * Makes the addresses a terminal prints clickable.
 *
 * **Why it is worth a file.** Every terminal in this product prints addresses somebody then wants
 * to visit — the dev server a folder command started, the pull request an agent opened, the
 * documentation page a build points at when it fails. Without this they are text: you select
 * across a wrapped line, miss the last character, and paste something that does not resolve. It is
 * a gap in the product rather than a piece of one feature, which is why it is wired into both
 * terminals from one place rather than into the screen that noticed it.
 *
 * **It is also the whole of how a folder's own application is reached.** #88 asked a checkout to
 * declare a line that prints its address, and #94 removed that: the process already prints one, and
 * a declared URL is right once — with two worktrees up the second Vite takes 5174, and a literal
 * would open the first one's application. What the tool printed in the terminal that started it is
 * the only answer that is true in both, and this is what makes it a click.
 *
 * <b>Our own handler rather than the addon's.</b> `new WebLinksAddon()` with no arguments calls
 * `window.open(uri)`, and a window opened that way keeps `window.opener` pointing back here — the
 * reverse tabnabbing the addon's own typings warn about, where the page that was opened can
 * navigate the one that opened it. `noopener` severs that, and `noreferrer` stops this app's URL,
 * which carries folder ids, from arriving as a `Referer` on somebody else's server.
 *
 * The scheme is checked as well as matched. The addon's default regex is http and https, so this
 * cannot fire on anything else today — but the guard is what keeps that true if the regex is ever
 * given to this function to widen, and `javascript:` reaching `window.open` is not a mistake worth
 * leaving one edit away.
 */
export function openWebLinks(terminal: Terminal): IDisposable {
  const unguard = guardLinkProviders(terminal);

  const addon = new WebLinksAddon((event, uri) => {
    // A modifier means the person is asking their browser for something else — a background tab, a
    // window, a save. Let it have the event rather than opening a tab they did not ask for.
    if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;

    let parsed: URL;
    try {
      parsed = new URL(uri);
    } catch {
      return;
    }

    if (parsed.protocol !== "http:" && parsed.protocol !== "https:") return;

    window.open(parsed.href, "_blank", "noopener,noreferrer");
  });

  // After the guard and before nothing else: `loadAddon` is where the addon registers the provider
  // that has to be wrapped.
  terminal.loadAddon(addon);

  return {
    dispose: () => {
      addon.dispose();
      unguard();
    },
  };
}

/**
 * Stops a link provider that throws from taking the page down with it.
 *
 * **Orca carries this and says why** (`terminal-link-provider-guard.ts`), and it is the one thing
 * from its whole `terminal-link-*` family that this product needs verbatim. xterm's own link
 * computer raises `RangeError: Invalid array length` on some pathological wrapped lines, and the
 * lines it means are agent-CLI output — which is the majority of what is written into the terminal
 * on the run screen. Providers are called **synchronously** from a mouse move, so the throw does
 * not land in a promise: it reaches `window.onerror`, and Chromium kills the renderer process. The
 * whole app disappears because somebody moved the pointer over a transcript.
 *
 * So `registerLinkProvider` is wrapped before the addon is loaded, which is what puts the addon's
 * own provider inside the net rather than only providers we wrote. A provider that throws answers
 * "no link here", which is the state the terminal was in before any of this was added.
 *
 * Returns the undo, because the patch is on the terminal instance and the terminal outlives one
 * addon.
 */
function guardLinkProviders(terminal: Terminal): () => void {
  const original = terminal.registerLinkProvider.bind(terminal);

  terminal.registerLinkProvider = (provider) =>
    original({
      provideLinks: (line, callback) => {
        // Whether the provider already answered. A guarded call that throws *after* calling back
        // must not answer twice — xterm treats the second answer as a second set of links.
        let answered = false;
        try {
          provider.provideLinks(line, (links) => {
            answered = true;
            callback(links);
          });
        } catch {
          if (!answered) callback(undefined);
        }
      },
    });

  return () => {
    terminal.registerLinkProvider = original;
  };
}
