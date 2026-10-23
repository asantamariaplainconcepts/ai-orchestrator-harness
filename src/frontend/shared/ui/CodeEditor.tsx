import { defaultKeymap, history, historyKeymap } from "@codemirror/commands";
import {
  bracketMatching,
  foldGutter,
  foldKeymap,
  HighlightStyle,
  indentOnInput,
  LanguageDescription,
  syntaxHighlighting,
} from "@codemirror/language";
import { languages } from "@codemirror/language-data";
import { highlightSelectionMatches, searchKeymap } from "@codemirror/search";
import { Compartment, EditorState } from "@codemirror/state";
import {
  drawSelection,
  EditorView,
  highlightActiveLine,
  highlightActiveLineGutter,
  highlightSpecialChars,
  keymap,
  lineNumbers,
} from "@codemirror/view";
import { tags } from "@lezer/highlight";
import { useEffect, useRef } from "react";

/**
 * What a reader and a small edit need, and no more: line numbers, folding, history, search and
 * bracket matching. `basicSetup` from the `codemirror` package would add autocompletion and lint,
 * which a file with no language server has nothing to feed.
 */
const setup = [
  lineNumbers(),
  highlightActiveLineGutter(),
  highlightSpecialChars(),
  history(),
  foldGutter(),
  drawSelection(),
  indentOnInput(),
  bracketMatching(),
  highlightActiveLine(),
  highlightSelectionMatches(),
  keymap.of([...defaultKeymap, ...historyKeymap, ...searchKeymap, ...foldKeymap]),
];

/**
 * A file in CodeMirror 6 (#514), themed by the design system's tokens so it swaps with the theme
 * like everything else. **Import it lazily**: CodeMirror is about a hundred kilobytes that only the
 * Files tab needs, and each language loads on its own when a file of that extension is opened.
 *
 * The document is the editor's: `text` seeds it, and a new `docKey` starts it over. A line ending of
 * CRLF in the text is kept, so saving a Windows file does not rewrite every line of it.
 */
export function CodeEditor({
  docKey,
  text,
  path,
  readOnly,
  label,
  onChange,
  onSave,
}: {
  docKey: string;
  text: string;
  path: string;
  readOnly: boolean;
  label: string;
  onChange: (text: string) => void;
  onSave: () => void;
}) {
  const host = useRef<HTMLDivElement>(null);
  const handlers = useRef({ onChange, onSave });
  handlers.current = { onChange, onSave };

  useEffect(() => {
    if (!host.current) return;
    const language = new Compartment();
    const view = new EditorView({
      parent: host.current,
      state: EditorState.create({
        doc: text,
        extensions: [
          setup,
          language.of([]),
          syntaxHighlighting(highlight),
          theme,
          text.includes("\r\n") ? EditorState.lineSeparator.of("\r\n") : [],
          EditorState.readOnly.of(readOnly),
          EditorView.editable.of(!readOnly),
          EditorView.contentAttributes.of({ "aria-label": label }),
          keymap.of([
            {
              key: "Mod-s",
              preventDefault: true,
              run: () => {
                handlers.current.onSave();
                return true;
              },
            },
          ]),
          EditorView.updateListener.of((update) => {
            if (update.docChanged) handlers.current.onChange(update.state.doc.toString());
          }),
        ],
      }),
    });

    let alive = true;
    void LanguageDescription.matchFilename(languages, path)
      ?.load()
      .then((support) => {
        if (alive) view.dispatch({ effects: language.reconfigure(support) });
      });

    return () => {
      alive = false;
      view.destroy();
    };
    // The editor starts over only when the document does: a new file, or the same file read again.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [docKey, readOnly]);

  return <div ref={host} style={{ height: "100%", minHeight: 0, overflow: "hidden" }} />;
}

/** The language CodeMirror reads a file as, by its name: `C#` for `Loop.cs`, or null for plain text. */
export function languageOf(path: string): string | null {
  return LanguageDescription.matchFilename(languages, path)?.name ?? null;
}

/** Syntax in the chart palette, which the dark theme redefines, so a keyword reads in both. */
const highlight = HighlightStyle.define([
  { tag: [tags.keyword, tags.controlKeyword, tags.modifier, tags.operatorKeyword], color: "var(--chart-1)" },
  { tag: [tags.string, tags.special(tags.string), tags.regexp, tags.character], color: "var(--chart-3)" },
  { tag: [tags.number, tags.bool, tags.null, tags.atom], color: "var(--chart-2)" },
  { tag: [tags.typeName, tags.className, tags.namespace, tags.propertyName, tags.attributeName], color: "var(--chart-5)" },
  { tag: [tags.comment, tags.meta, tags.processingInstruction], color: "var(--muted-foreground)" },
  { tag: tags.heading, fontWeight: "var(--font-weight-semibold)" },
  { tag: tags.link, color: "var(--chart-1)", textDecoration: "underline" },
  { tag: tags.invalid, color: "var(--chart-4)" },
]);

const theme = EditorView.theme({
  "&": {
    height: "100%",
    color: "var(--foreground)",
    backgroundColor: "var(--background)",
    fontSize: "var(--text-xs)",
  },
  "&.cm-focused": { outline: "2px solid var(--ring)", outlineOffset: "-2px" },
  ".cm-scroller": { fontFamily: "var(--font-mono)", lineHeight: "1.65" },
  ".cm-content": { caretColor: "var(--foreground)", padding: "8px 0" },
  ".cm-cursor, .cm-dropCursor": { borderLeftColor: "var(--foreground)" },
  ".cm-gutters": {
    backgroundColor: "var(--background)",
    color: "var(--muted-foreground)",
    border: "none",
  },
  ".cm-activeLine, .cm-activeLineGutter": { backgroundColor: "var(--muted)" },
  "&.cm-focused > .cm-scroller > .cm-selectionLayer .cm-selectionBackground, .cm-selectionBackground, .cm-content ::selection":
    { backgroundColor: "var(--accent)" },
  ".cm-searchMatch": { backgroundColor: "color-mix(in srgb, var(--warning) 30%, transparent)" },
  ".cm-matchingBracket, .cm-nonmatchingBracket": { backgroundColor: "var(--secondary)" },
  ".cm-foldPlaceholder": {
    backgroundColor: "var(--muted)",
    color: "var(--muted-foreground)",
    border: "1px solid var(--border)",
  },
  ".cm-panels": {
    backgroundColor: "var(--card)",
    color: "var(--foreground)",
    borderColor: "var(--border)",
  },
  ".cm-tooltip": {
    backgroundColor: "var(--popover)",
    color: "var(--popover-foreground)",
    border: "1px solid var(--border)",
  },
});
