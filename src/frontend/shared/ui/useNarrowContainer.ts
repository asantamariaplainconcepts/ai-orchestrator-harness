import { useEffect, useState } from "react";

/**
 * Whether the element this returns a ref-setter for is narrower than `width`.
 *
 * **A container query in a codebase that styles inline**, and the shape is
 * [`RunScreen`](../../features/runs/RunScreen.tsx)'s `useCompact`, which established it and says why
 * the container is the right thing to measure: the projects rail sits between the window and a
 * screen's panes, and a pane has no business knowing whether the rail is open. A `matchMedia`
 * breakpoint would flip a panel's layout because somebody opened the rail.
 *
 * **The element arrives through state rather than through a ref.** Every caller renders `Loading` or
 * an empty state before it renders the thing to measure, so an effect keyed on `[]` would read a null
 * ref once and never look again — and the layout would stay wide forever. A `useState` setter is a
 * valid ref callback and re-runs the effect when the node appears.
 *
 * **Zero is unmeasured, not narrow.** A pane with no layout yet reports 0, and treating that as
 * narrow flips the layout once on every mount — the same mistake I4 caught the terminal making with
 * its own size.
 *
 * There is a third copy of this in `RunScreen.tsx`, deliberately left alone: it is a file this change
 * does not own, and moving code out of somebody else's file is the conflict no ownership record
 * predicts (AGENTS.md, *What collides*). Folding it in here is its own small change.
 */
export function useNarrowContainer<T extends HTMLElement>(
  width: number,
): [(node: T | null) => void, boolean] {
  const [host, setHost] = useState<T | null>(null);
  const [narrow, setNarrow] = useState(false);

  useEffect(() => {
    const element = host;
    if (!element) return;
    const observer = new ResizeObserver(() => {
      const measured = element.clientWidth;
      if (measured > 0) setNarrow(measured < width);
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, [host, width]);

  return [setHost, narrow];
}
