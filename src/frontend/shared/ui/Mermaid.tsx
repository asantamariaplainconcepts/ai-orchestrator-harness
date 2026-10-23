import { useEffect, useLayoutEffect, useRef, useState, type RefObject } from "react";
import { t } from "@/shared/i18n";
import type { StatusPalette } from "./mermaidOverlay";

/** What the diagram is painted with, every value a token's computed colour, and its identity as one string. */
interface Tokens {
  key: string;
  theme: Record<string, string>;
  status: StatusPalette;
}

/**
 * #611. A ```` ```mermaid ```` block drawn as its diagram, with the `mermaid` library fetched the
 * first time a block is on screen and not before.
 *
 * The theme is Mermaid's `base`, fed the computed values of DESIGN.md's tokens on this element, so
 * the diagram changes with the tokens and never reads which theme is on. `transform` rewrites the
 * text before drawing, as the Issue tab's live overlay does; it is handed the same tokens' palette.
 * The diagram is drawn again only when the text it would draw changes: a poll that brings the same
 * states draws nothing.
 *
 * A diagram that does not parse is shown as its code with Mermaid's error under it, so a typo in an
 * issue costs that block and not the tab.
 */
export function Mermaid({
  text,
  transform,
}: {
  text: string;
  transform?: (text: string, palette: StatusPalette) => string;
}) {
  const box = useRef<HTMLElement>(null);
  const tokens = useTokens(box);
  const source = tokens === null ? null : transform ? transform(text, tokens.status) : text;
  const [drawn, setDrawn] = useState<{ source: string; key: string; svg?: string; error?: string } | null>(null);

  useEffect(() => {
    if (source === null || tokens === null) return;
    let current = true;
    void draw(source, tokens.theme).then((result) => {
      if (current) setDrawn({ source, key: tokens.key, ...result });
    });
    return () => {
      current = false;
    };
  }, [source, tokens]);

  return (
    <figure ref={box} aria-label={t("mermaid.label")} className="aio-mermaid">
      {drawn?.error !== undefined ? (
        <>
          <pre>
            <code>{text}</code>
          </pre>
          <figcaption role="alert" className="aio-mermaid__error">
            {t("mermaid.failed", { reason: drawn.error })}
          </figcaption>
        </>
      ) : drawn?.svg !== undefined ? (
        <div
          className="aio-mermaid__svg"
          style={{ minWidth: smallest(drawn.svg) }}
          dangerouslySetInnerHTML={{ __html: drawn.svg }}
        />
      ) : (
        <span className="aio-mermaid__drawing">{t("mermaid.drawing")}</span>
      )}
    </figure>
  );
}

/**
 * How narrow a diagram may be drawn: 60% of its own width. It shrinks to fit the pane down to that,
 * and below it the figure scrolls, because #610's graph squeezed into 390px measured 330px wide with
 * its labels at about 4px. At 60% the same graph still fits the Issue tab at 1440px.
 */
function smallest(svg: string): number | undefined {
  const width = Number(/viewBox="[-\d.]+ [-\d.]+ ([\d.]+)/.exec(svg)?.[1]);
  return Number.isFinite(width) && width > 0 ? Math.round(width * 0.6) : undefined;
}

/**
 * The tokens as this element computes them, read again whenever a class changes anywhere in the
 * document, which is how the theme switches. The object changes only when a value does.
 */
function useTokens(box: RefObject<HTMLElement | null>): Tokens | null {
  const [tokens, setTokens] = useState<Tokens | null>(null);

  useLayoutEffect(() => {
    const read = () => {
      if (!box.current) return;
      const next = measure(box.current);
      setTokens((previous) => (previous?.key === next.key ? previous : next));
    };
    read();
    const observer = new MutationObserver(read);
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ["class"], subtree: true });
    return () => observer.disconnect();
  }, [box]);

  return tokens;
}

function measure(element: HTMLElement): Tokens {
  const style = getComputedStyle(element);
  const token = (name: string) => style.getPropertyValue(`--${name}`).trim();
  const theme = {
    background: token("card"),
    fontFamily: token("font-sans"),
    fontSize: "14px",
    textColor: token("foreground"),
    titleColor: token("foreground"),
    primaryColor: token("muted"),
    primaryTextColor: token("foreground"),
    primaryBorderColor: token("input"),
    mainBkg: token("muted"),
    nodeBorder: token("input"),
    secondaryColor: token("accent"),
    secondaryTextColor: token("accent-foreground"),
    secondaryBorderColor: token("input"),
    tertiaryColor: token("secondary"),
    tertiaryTextColor: token("foreground"),
    tertiaryBorderColor: token("border"),
    lineColor: token("muted-foreground"),
    clusterBkg: token("card"),
    clusterBorder: token("border"),
    edgeLabelBackground: token("card"),
  };
  const status: StatusPalette = {
    done: { fill: token("success"), stroke: token("success"), color: token("success-foreground") },
    running: { fill: token("primary"), stroke: token("primary"), color: token("primary-foreground") },
    held: { fill: token("warning"), stroke: token("warning"), color: token("warning-foreground") },
    waiting: { fill: token("accent"), stroke: token("primary"), color: token("accent-foreground") },
    plain: { fill: token("muted"), stroke: token("input"), color: token("foreground") },
  };
  return { key: JSON.stringify([theme, status]), theme, status };
}

// Mermaid is configured globally, so one diagram draws at a time: two drawn at once would each be
// painted with whichever `initialize` ran last.
let queue: Promise<unknown> = Promise.resolve();
let drawings = 0;

function draw(source: string, theme: Record<string, string>): Promise<{ svg?: string; error?: string }> {
  const next = queue.then(async () => {
    const { default: mermaid } = await import("mermaid");
    mermaid.initialize({
      startOnLoad: false,
      theme: "base",
      themeVariables: theme,
      securityLevel: "strict",
      suppressErrorRendering: true,
    });
    const id = `aio-mermaid-${++drawings}`;
    try {
      const { svg } = await mermaid.render(id, source);
      return { svg };
    } catch (failure) {
      // A failed render can leave its scratch element in the body.
      document.getElementById(`d${id}`)?.remove();
      document.getElementById(id)?.remove();
      return { error: failure instanceof Error ? failure.message : String(failure) };
    }
  });
  queue = next.catch(() => undefined);
  return next;
}
