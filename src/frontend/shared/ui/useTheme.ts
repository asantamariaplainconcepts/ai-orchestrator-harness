import { useEffect, useState } from "react";

export type Theme = "light" | "dark";

const STORAGE_KEY = "aio.theme";

/**
 * Light or dark, following the OS until somebody chooses otherwise.
 *
 * The design system is class-based — `.dark` on an ancestor swaps every semantic token — so the
 * app never branches on the theme, it only puts the class somewhere. That is the whole contract:
 * a component that read the theme to pick a colour would be the drift the tokens exist to prevent.
 *
 * The choice is stored, because a person who picked dark did not mean "until I close the tab".
 */
export function useTheme(): [Theme, () => void] {
  const [theme, setTheme] = useState<Theme>(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === "light" || stored === "dark") return stored;
    return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
  });

  // Follow the OS while nothing has been chosen. Once it has, the stored value wins and this
  // listener stops mattering — which is why it checks storage rather than unsubscribing.
  useEffect(() => {
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const onChange = (event: MediaQueryListEvent) => {
      if (!localStorage.getItem(STORAGE_KEY)) setTheme(event.matches ? "dark" : "light");
    };
    media.addEventListener("change", onChange);
    return () => media.removeEventListener("change", onChange);
  }, []);

  const toggle = () => {
    setTheme((current) => {
      const next = current === "dark" ? "light" : "dark";
      localStorage.setItem(STORAGE_KEY, next);
      return next;
    });
  };

  return [theme, toggle];
}
