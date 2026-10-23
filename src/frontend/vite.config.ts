import react from "@vitejs/plugin-react";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath, URL } from "node:url";
import { gzipSync } from "node:zlib";
import { defineConfig, type Plugin } from "vite";

// The build output lands in the host's wwwroot: the SPA is served same-origin in production,
// exactly as the Aspire dev proxy serves it in development. No CORS, no absolute API base URL.
const apiProxyTarget = process.env.API_PROXY_TARGET ?? "http://localhost:5080";

/**
 * What the browser has to have before it can draw anything, in bytes over the wire.
 *
 * 200 kB gzip, against a measured 313.87 the day this was written. The 132.87 that came out are the
 * terminal emulator and the Markdown pipeline, neither of which draws Folders, Tasks or History —
 * they are fetched when somebody opens a terminal, a run, or a Markdown file.
 *
 * **The one number here that does not depend on the machine.** Every other reading this change
 * produces — navigation timing, long tasks, commit durations — is a fact about the computer that
 * took it, and `AGENTS.md` says what a gate born red teaches people to do: read past it. Bytes out
 * of a build are the same on every machine, so bytes are what gets to be a gate.
 *
 * Back to 200 kB with #518, after a stretch at 205 while the old screens and the v2 ones shared one
 * `en.ts`. With the old tree gone the first screen measured 120.21 kB: the shell, its rail and the
 * copy, with every screen behind its lazy route.
 */
const entryBudgetGzip = 200 * 1024;

/**
 * Fail the build when the first screen costs more than {@link entryBudgetGzip}.
 *
 * **It measures the entry chunk and everything statically reachable from it, not the entry file.**
 * Measuring one file would let the tool sitting directly above this — `manualChunks` — switch the
 * gate off: moving React into a chunk of its own drops the entry *file* by 60 kB gzip while the
 * browser still downloads every byte before first paint. The static closure is what a cold load
 * actually costs, and it cannot be argued down by spreading the same weight over more files.
 *
 * Dynamic imports are excluded, which is the whole point of the boundaries this gate is here to
 * defend: a chunk fetched when a terminal tab opens is not part of drawing the screen that has no
 * terminal on it.
 *
 * Measured from the files on disk rather than from the bundle in memory, so the number is the one
 * the server will serve. Gzip rather than raw because gzip is what crosses the wire, and it is the
 * unit the reading this budget was set against was taken in.
 */
function entryBudget(limit: number): Plugin {
  return {
    name: "aio-entry-budget",
    apply: "build",
    writeBundle(options, bundle) {
      const dir = options.dir;
      if (!dir) return;

      const entry = Object.values(bundle).find(
        (output) => output.type === "chunk" && output.isEntry,
      );
      if (!entry || entry.type !== "chunk") return;

      // Breadth-first over static imports only. `imports` is what the chunk needs to evaluate;
      // `dynamicImports` is what it may later ask for, and that is the difference being bought.
      const reached = new Set<string>();
      const queue = [entry.fileName];
      while (queue.length > 0) {
        const fileName = queue.pop();
        if (fileName === undefined || reached.has(fileName)) continue;
        reached.add(fileName);

        const output = bundle[fileName];
        if (output?.type === "chunk") queue.push(...output.imports);
      }

      let gzip = 0;
      for (const fileName of reached) gzip += gzipSync(readFileSync(join(dir, fileName))).length;

      const kb = (bytes: number) => `${(bytes / 1024).toFixed(2)} kB`;
      const summary = `${reached.size} chunk${reached.size === 1 ? "" : "s"}, ${kb(gzip)} gzip`;

      if (gzip > limit) {
        this.error(
          `The first screen costs ${summary}, over the ${kb(limit)} budget.` +
            ` What loads before anything is drawn: ${[...reached].sort().join(", ")}.` +
            ` Either the new weight belongs behind a lazy() boundary, or the budget in` +
            ` vite.config.ts is wrong and moving it is the change being proposed.`,
        );
      }

      this.info(`first screen: ${summary}, within the ${kb(limit)} budget`);
    },
  };
}

export default defineConfig({
  plugins: [react(), entryBudget(entryBudgetGzip)],
  // Mirrors the "@/*" paths mapping in tsconfig.json — both resolvers must agree.
  resolve: {
    alias: { "@": fileURLToPath(new URL(".", import.meta.url)) },
  },
  build: {
    outDir: "../root/AiOrchestrator.Server/wwwroot",
    emptyOutDir: true,
    rollupOptions: {
      output: {
        /**
         * The libraries every screen needs, kept apart from the code that changes.
         *
         * **Not a way to make the budget above pass** — it cannot be, because that budget measures
         * this chunk and the entry together. It is about the second visit: React, the router and the
         * query client move when their versions do, and the app's own code moves several times a
         * day, so a person who reloads after a change re-downloads 56 kB instead of 176.
         *
         * An explicit list rather than "everything in node_modules". A rule that swept the whole
         * folder would pull `@xterm/xterm` and `react-markdown` — reached only through the `lazy()`
         * boundaries this change adds — back into a chunk loaded before first paint, undoing the
         * change while appearing to tidy it. Nothing lands here unless it is named.
         */
        manualChunks(id) {
          return /[\\/]node_modules[\\/](react|react-dom|scheduler|react-router|@tanstack)[\\/]/.test(
            id,
          )
            ? "react"
            : undefined;
        },
      },
    },
  },
  server: {
    port: Number(process.env.PORT ?? 5173),
    strictPort: true,
    // Where this dev server sends `/api`, from the AppHost when there is one. Under `aspire start`
    // the Server proxies to this dev server rather than the other way round, so this block is only
    // reached when somebody opens the Vite origin directly — which is exactly what a person does
    // when the Server's own origin is broken, so it has to be right in the failure case above all.
    //
    // It was `http://localhost:5080` written out: the port in the Server's launch profile, which
    // `aspire start --isolated` does not use. With two isolated worktrees up at once that 5080 is
    // the *other* worktree's application, and this proxy served its data without a word. The
    // AppHost sets API_PROXY_TARGET to the endpoint it actually allocated for this instance's
    // Server; the literal stays as the fallback because standalone `pnpm dev` really does talk to a
    // Server started by hand, and 5080 is where its launch profile puts it.
    proxy: {
      "/api": { target: apiProxyTarget, changeOrigin: true, ws: true },
    },
  },
});
