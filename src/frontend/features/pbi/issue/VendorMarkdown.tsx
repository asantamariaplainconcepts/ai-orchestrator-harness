import Markdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { t } from "@/shared/i18n";
import { Mermaid } from "@/shared/ui/Mermaid";
import type { StatusPalette } from "@/shared/ui/mermaidOverlay";

/** An image as markdown names it and where this screen loads it from. */
export interface Picture {
  src: string;
  alt: string;
}

/**
 * Markdown as the vendor or a change wrote it, with its images inside (#512,
 * DESIGN.md, "Issue and Proposal show what is elsewhere").
 *
 * Three things differ from `Prose`. An image's address is handed to `image`, which says where this
 * origin serves it from, because the vendor serves a private repository's images only to its own
 * credential and a change's images live on a branch. An image is a button that enlarges it. And an
 * HTML `<img>` tag, which GitHub writes for a resized screenshot, becomes a markdown image so it
 * is drawn rather than dropped; every other piece of HTML, the harness's `<!-- aio:… -->` records
 * among it, is not rendered.
 *
 * Its headings sit one level under the screen's: `#` is an `h2`, down to `h6`, so the screen keeps
 * its one `h1` (#586). The stylesheet sizes `.markdown-preview` headings by the same shift.
 *
 * A ```` ```mermaid ```` block is drawn as its diagram (#611), and `diagram` may rewrite its text
 * first, as the Issue tab does to paint an epic's sub-issues with their live state.
 *
 * Imported only by the Issue and Proposal tabs, which are lazy, so the renderer is paid for when one
 * of them opens.
 */
export function VendorMarkdown({
  text,
  image,
  onEnlarge,
  diagram,
}: {
  text: string;
  /** Where this screen loads an image named by `src` from. */
  image: (src: string) => string;
  onEnlarge: (picture: Picture) => void;
  /** Rewrites a Mermaid block's text before it is drawn, with the palette of the tokens. */
  diagram?: (text: string, palette: StatusPalette) => string;
}) {
  return (
    <div className="markdown-preview markdown-preview--flush">
      <Markdown
        remarkPlugins={[remarkGfm]}
        components={{
          a: (props) => <a {...props} target="_blank" rel="noreferrer" />,
          h1: ({ children }) => <h2>{children}</h2>,
          h2: ({ children }) => <h3>{children}</h3>,
          h3: ({ children }) => <h4>{children}</h4>,
          h4: ({ children }) => <h5>{children}</h5>,
          h5: ({ children }) => <h6>{children}</h6>,
          img: ({ src, alt }) => {
            if (typeof src !== "string" || src === "") return null;
            const picture = { src: image(src), alt: alt ?? "" };
            return (
              <button
                type="button"
                className="aio-image"
                aria-label={t("image.enlarge", { name: picture.alt || t("image.unnamed") })}
                onClick={() => onEnlarge(picture)}
              >
                <img src={picture.src} alt={picture.alt} loading="lazy" style={{ maxHeight: 360 }} />
              </button>
            );
          },
          pre: ({ node, children, ...props }) => {
            const code = node?.children[0];
            if (code?.type === "element" && code.tagName === "code") {
              const classes = code.properties.className;
              if (Array.isArray(classes) && classes.includes("language-mermaid")) {
                const source = code.children.map((child) => (child.type === "text" ? child.value : "")).join("");
                return <Mermaid text={source} transform={diagram} />;
              }
            }
            return <pre {...props}>{children}</pre>;
          },
          table: (props) => (
            <div style={{ maxWidth: "100%", overflowX: "auto" }}>
              <table {...props} />
            </div>
          ),
        }}
      >
        {readable(text)}
      </Markdown>
    </div>
  );
}

/** HTML comments out, `<img>` tags in as markdown images. */
export function readable(text: string): string {
  return text
    .replace(/<!--[\s\S]*?-->/g, "")
    .replace(/<img\b[^>]*>/gi, (tag) => {
      const src = /\bsrc\s*=\s*"([^"]*)"/i.exec(tag)?.[1];
      const alt = /\balt\s*=\s*"([^"]*)"/i.exec(tag)?.[1] ?? "";
      return src ? `![${alt.replace(/[[\]]/g, "")}](${src})` : "";
    });
}
