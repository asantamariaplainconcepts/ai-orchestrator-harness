import { useEffect, useRef } from "react";
import { useLocation, useNavigate } from "react-router";
import { usePreferences } from "@/shared/http/settings";

/**
 * #680. Opens the requirements screen when the console is opened at its root, as a start opens it,
 * and the preferences say it has not been seen with every required CLI present. A link to a screen
 * is followed as it is, and the check runs once per load, so a person who leaves the screen is not
 * sent back on the next click. Only an explicit `false` counts: an answer without the field opens
 * nothing.
 */
export function useFirstStart() {
  const preferences = usePreferences();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const openedAt = useRef(pathname);
  const asked = useRef(false);
  const unseen = preferences.data?.requirementsSeen === false;

  useEffect(() => {
    if (asked.current || preferences.data === undefined) return;
    asked.current = true;
    if (unseen && openedAt.current === "/") {
      void navigate("/requirements", { replace: true, state: { firstStart: true } });
    }
  }, [preferences.data, unseen, navigate]);
}
