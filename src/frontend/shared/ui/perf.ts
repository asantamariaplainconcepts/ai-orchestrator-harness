/**
 * What this app spends the main thread on, said in numbers, in development only.
 *
 * **Nobody had ever measured this frontend.** There is no test for it and `pnpm build` checks that
 * it compiles, not that it is quick, so every claim about it being slow was an adjective. The five
 * candidate causes have different symptoms and wildly different fixes — a single chunk, a render
 * cascade out of `AppShell`, hover held in React state, a tab unmounting, xterm rebuilding — and
 * without a reading there is no way to tell which one somebody is actually looking at.
 *
 * This writes to the console rather than anywhere durable, because the reader is a person with the
 * app open in front of them. A number kept in a file would be a number about a machine, and the
 * only machine whose answer matters is the one somebody is complaining about.
 *
 * **Development only, and structurally rather than by promise.** Every entry point here is guarded
 * on `import.meta.env.DEV`, and every call site guards again, so a production build leaves these
 * functions unreferenced and the module is shaken out whole — `grep -c longtask` over the built
 * bundle is 0. A performance observer that shipped would be an observer costing the person time in
 * order to tell them how much time they are spending.
 *
 * Deliberately not a test. An assertion on a millisecond figure is an assertion about the machine
 * that ran it, and `AGENTS.md` says what a gate born red teaches people to do: read past it. The one
 * number here that does not depend on the machine is the size of the bundle, and that one *is* a
 * gate, in `vite.config.ts`.
 */

const label = "[perf]";

/** Whether the entry type is one this browser will actually report, so asking cannot throw. */
function observes(type: string): boolean {
  return PerformanceObserver.supportedEntryTypes?.includes(type) ?? false;
}

/**
 * The baseline: how long the first screen took, and the first long task that got in its way.
 *
 * **This is the reading that did not exist.** The bundle's 313.87 kB gzip is bytes, and bytes are a
 * proxy — what somebody opening the app experiences is milliseconds, and splitting the chunk can
 * only be judged against a figure taken before it was split. `navigation` is the browser's own
 * account of the cold load and needs no instrumentation to exist; the first long task is what says
 * whether the main thread was still busy after the load event said it was done.
 *
 * Logged once, on a cold load of the Server's origin. A reload that hits the cache is a different
 * question and says so in the numbers.
 */
export function reportNavigation(): void {
  if (!import.meta.env.DEV) return;

  // **After the load event, not when this is called.** `main.tsx` runs while the document is still
  // loading, and at that point the navigation entry exists but `domContentLoadedEventEnd`,
  // `loadEventEnd` and `duration` are all still 0, because the events they time have not happened.
  // Read eagerly, the baseline this exists to write down prints as three zeros — which is how this
  // was caught. The extra `setTimeout` is because `loadEventEnd` is only stamped once every load
  // handler has returned, so reading inside one is reading it one tick early.
  if (document.readyState === "complete") {
    setTimeout(logNavigation, 0);
    return;
  }

  window.addEventListener("load", () => setTimeout(logNavigation, 0), { once: true });
}

function logNavigation(): void {
  const [navigation] = performance.getEntriesByType("navigation") as PerformanceNavigationTiming[];
  if (!navigation) return;

  // `responseEnd` is where the network stops being the story and the main thread starts being it —
  // the interesting half of this app's cold load is everything after it, because the bundle arriving
  // from localhost costs almost nothing and parsing it costs the rest.
  console.info(
    `${label} navigation ${Math.round(navigation.duration)}ms` +
      ` · response ${Math.round(navigation.responseEnd)}ms` +
      ` · dom ${Math.round(navigation.domContentLoadedEventEnd)}ms` +
      ` · load ${Math.round(navigation.loadEventEnd)}ms` +
      ` · transfer ${navigation.transferSize}B`,
  );
}

/**
 * Every task that blocked the main thread for longer than the browser is willing to ignore.
 *
 * 50ms is the browser's threshold, not a choice made here, and it is the right one: a frame is
 * 16.7ms, so a task over 50 has already cost three of them. The first one is called out separately
 * because it belongs to the cold load and completes {@link reportNavigation}'s half of the baseline;
 * after that what matters is the shape of the stream — dragging the rail, moving the pointer down
 * it, switching tabs with a live agent — so each entry says when it happened and for how long.
 *
 * Returns its own teardown. Nothing calls it today, because the observer is meant to outlive every
 * screen and the app has exactly one shell, but a hook that leaked an observer per mount would be
 * an instrument that changed what it measures.
 */
export function observeLongTasks(): () => void {
  if (!import.meta.env.DEV || !observes("longtask")) return () => {};

  let seen = 0;
  const observer = new PerformanceObserver((list) => {
    for (const entry of list.getEntries()) {
      seen += 1;
      const at = Math.round(entry.startTime);
      const took = Math.round(entry.duration);
      if (seen === 1) {
        console.info(`${label} first long task ${took}ms at ${at}ms — the rest of the baseline`);
      } else {
        console.info(`${label} long task ${took}ms at ${at}ms (${seen} so far)`);
      }
    }
  });

  observer.observe({ type: "longtask", buffered: true });
  return () => observer.disconnect();
}

/**
 * What one commit of the screen under the outlet cost, and whether it was mounting or updating.
 *
 * The `AppShell` → `<Outlet/>` boundary is the one place in this app where a memo would stop an
 * entire cascade, and it is the boundary this measures rather than the one it changes. `AppShell`
 * subscribes to five queries — two of them polling at 3s and 4s — and draws every screen through the
 * outlet with no memo between them, so a live run's changing summary repaints `PbisScreen` (2,146
 * lines), `FolderDetailScreen` (1,133) or `RunScreen` (1,122) whole. Whether that is what somebody
 * feels is a question with an answer, and putting a `memo` there before reading the answer is its
 * own kind of bug.
 *
 * `actualDuration` is the real cost of the commit; `baseDuration` is what it would have cost with no
 * memoisation anywhere. The two being equal is the measurement, not a coincidence: this tree has
 * zero `memo()` in it.
 */
export function reportOutletRender(
  id: string,
  phase: "mount" | "update" | "nested-update",
  actualDuration: number,
  baseDuration: number,
): void {
  if (!import.meta.env.DEV) return;

  // Sub-millisecond commits are the quiet ones, and printing them buries the ones that are not.
  if (actualDuration < 1) return;

  console.info(
    `${label} ${id} ${phase} ${actualDuration.toFixed(1)}ms` +
      ` (unmemoised ${baseDuration.toFixed(1)}ms)`,
  );
}
