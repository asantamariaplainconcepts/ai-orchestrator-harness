import { QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { RouterProvider } from "react-router";
import { queryClient } from "@/shared/query/queryClient";
import { observeLongTasks, reportNavigation } from "@/shared/ui/perf";
import "@/styles/tokens.css";
import "@/styles/components.css";
import "@/styles/app.css";
import { router } from "./routes";

// What the cold load cost, and what blocks the main thread afterwards. Guarded here as well as
// inside the module so that a production build leaves both functions unreferenced and shakes the
// module out whole: `grep -c longtask` over the built bundle is 0.
if (import.meta.env.DEV) {
  reportNavigation();
  observeLongTasks();
}

// #517. The service worker that makes the console installable as an app (public/sw.js). Not in
// the dev loop, where Vite serves the page and a worker would outlive the server it came from.
if (!import.meta.env.DEV && "serviceWorker" in navigator) {
  void navigator.serviceWorker.register("/sw.js");
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
);
