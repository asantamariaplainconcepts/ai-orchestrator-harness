import { createBrowserRouter } from "react-router";
import { RouteErrorElement } from "./RouteError";
import { Layout } from "./Layout";
import { screens } from "./screens";

/**
 * The console's routes: one shell (rail, content, session tray) and the screens beneath it. The
 * shell is drawn on every screen, so it is in the first screen's byte budget (vite.config.ts); each
 * screen is lazy, in `screens.tsx`.
 */
export const router = createBrowserRouter([
  {
    path: "/",
    // One boundary for every route beneath the shell: a screen whose own module died loads this
    // rather than the router's default "Hey developer". What it heals and how it says it lives in
    // the component.
    errorElement: <RouteErrorElement />,
    element: <Layout />,
    children: screens,
  },
]);
