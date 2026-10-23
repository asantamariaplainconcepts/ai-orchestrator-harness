import { Check } from "lucide-react";
import { Fragment, useEffect, useRef, useState } from "react";
import type { ButtonHTMLAttributes, ReactNode, SelectHTMLAttributes } from "react";
import { t, type TranslationKey } from "@/shared/i18n";

/**
 * The kit: thin wrappers over the vendored `.pc-*` classes.
 *
 * They exist so screens compose components rather than remember class names, and so the one
 * mapping from "primary button" to `.pc-btn .pc-btn--default` lives in a single place. There is
 * no styling here that the design system does not already define — a wrapper that invented a
 * value would be exactly the drift the vendored CSS exists to prevent.
 */

type ButtonVariant = "default" | "outline" | "ghost" | "secondary" | "destructive";
type ButtonSize = "xs" | "sm" | "md" | "lg" | "icon-xs" | "icon-sm" | "icon";

const sizeClass: Record<ButtonSize, string> = {
  xs: "pc-btn--xs",
  sm: "pc-btn--sm",
  md: "",
  lg: "pc-btn--lg",
  "icon-xs": "pc-btn--icon-xs",
  "icon-sm": "pc-btn--icon-sm",
  icon: "pc-btn--icon",
};

export function Button({
  variant = "default",
  size = "md",
  className,
  ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: ButtonVariant; size?: ButtonSize }) {
  return (
    <button
      type="button"
      {...rest}
      className={["pc-btn", `pc-btn--${variant}`, sizeClass[size], className]
        .filter(Boolean)
        .join(" ")}
    />
  );
}

/**
 * One choice in a menu, whether a {@link SplitButton} drops it or a {@link MenuButton} does.
 *
 * `selected` marks the one the main button would do, so the menu says what the current default is
 * instead of only offering to change it — otherwise a row of four editors gives no clue which one
 * "Open in" is about to launch.
 *
 * `icon` takes the same gutter as the check mark, for a menu whose items are actions rather than
 * settings: nothing in an "add a folder / create worktree" menu is ever the selected one, and an
 * empty column reads as a mistake.
 *
 * `disabled` is `aria-disabled` and not the DOM attribute, deliberately. A disabled button is
 * removed from the tab order, which takes `title` — the only place the reason is written — with
 * it. The choice stays focusable, says it cannot be taken, and does nothing when taken.
 */
export interface MenuChoice {
  key: string;
  label: string;
  hint?: string;
  icon?: ReactNode;
  selected?: boolean;
  disabled?: boolean;
  /** Why, when `disabled`. Read on hover and on focus. */
  title?: string;
  /**
   * `destructive` colours the item that deletes something.
   *
   * A menu has no room for a warning sentence, so the colour is the whole of it: an item that reads
   * exactly like the two above it is one somebody picks by accident. It pairs with `separated`,
   * because adjacency is the other half of that mistake.
   */
  tone?: "default" | "destructive";
  /** Draw a rule above this item, to break it off from the group before it. */
  separated?: boolean;
  onSelect: () => void;
  /**
   * A second control on the row, beside the label rather than instead of it.
   *
   * The header's hold menu is why this exists: its rows navigate to the project a hold is in, and
   * the hold also has to be releasable from there — two intentions about one row, and a menu that
   * folded them into one item would make going to look at the work indistinguishable from approving
   * it. Rendered as a **sibling** of the `menuitem` button, never inside it: a button within a
   * button is invalid, and the inner one would not receive the click.
   *
   * Taking the action does not close the menu. The control is inside the box `useDismiss` watches,
   * so a press reaches it rather than dismissing, and a refusal stays readable on the row it belongs
   * to.
   */
  action?: ReactNode;
}

/**
 * Close on a click anywhere else, or on Escape.
 *
 * Bound to the document rather than to the menu's own blur: a click on the page behind it must
 * close it, and `blur` does not fire for a click on a non-focusable element. The ref it returns
 * goes on the box holding *both* the trigger and the popover — a click on the trigger while the
 * menu is open must reach the trigger's own toggle, and would instead be a dismiss if the trigger
 * were outside.
 */
function useDismiss(open: boolean, setOpen: (open: boolean) => void) {
  const box = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    const dismiss = (event: MouseEvent) => {
      if (!box.current?.contains(event.target as Node)) setOpen(false);
    };
    const escape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };

    document.addEventListener("mousedown", dismiss);
    document.addEventListener("keydown", escape);
    return () => {
      document.removeEventListener("mousedown", dismiss);
      document.removeEventListener("keydown", escape);
    };
  }, [open, setOpen]);

  return box;
}

/**
 * The list a menu drops. One copy, because two controls drop it.
 *
 * `align` is which edge it hangs from. A menu opened by a control sitting against the right of a
 * 264px rail cannot hang left: it would leave the rail, and the rail is what it belongs to.
 */
function MenuList({
  label,
  choices,
  align = "start",
  drop = "down",
  anchor,
  onDone,
}: {
  label: string;
  choices: MenuChoice[];
  align?: "start" | "end";
  drop?: "down" | "up";
  /** The trigger's box, for a list drawn `fixed` so a scrolling ancestor cannot clip it. */
  anchor?: DOMRect;
  onDone: () => void;
}) {
  return (
    <div
      role="menu"
      aria-label={label}
      style={{
        ...(anchor
          ? {
              position: "fixed",
              top: anchor.bottom + 4,
              // From the trigger's left edge, kept on screen: the list is 200px at least.
              left: Math.max(8, Math.min(anchor.left, window.innerWidth - 208)),
              // Nothing contains a fixed list, so a long hint would draw it the width of the screen.
              maxWidth: 280,
            }
          : {
              position: "absolute",
              ...(drop === "up" ? { bottom: "calc(100% + 4px)" } : { top: "calc(100% + 4px)" }),
              ...(align === "end" ? { right: 0 } : { left: 0 }),
            }),
        zIndex: 40,
        minWidth: 200,
        padding: 4,
        display: "flex",
        flexDirection: "column",
        gap: 1,
        border: "1px solid var(--border)",
        borderRadius: "var(--radius-lg)",
        background: "var(--popover, var(--card))",
        boxShadow: "0 8px 24px rgba(0, 0, 0, 0.16)",
      }}
    >
      {choices.map((choice) => (
        <Fragment key={choice.key}>
          {choice.separated ? (
            <div
              aria-hidden="true"
              style={{ height: 1, margin: "3px 0", background: "var(--border)" }}
            />
          ) : null}
          <div style={{ display: "flex", alignItems: "flex-start", gap: 2 }}>
          <button
            type="button"
            role="menuitem"
            aria-disabled={choice.disabled || undefined}
            title={choice.title}
            onClick={() => {
              if (choice.disabled) return;
              onDone();
              choice.onSelect();
            }}
            style={{
              display: "flex",
              alignItems: "center",
              gap: 8,
              width: "100%",
              padding: "6px 8px",
              border: 0,
              borderRadius: "var(--radius-md)",
              background: choice.selected ? "var(--accent)" : "transparent",
              color: choice.tone === "destructive" ? "var(--destructive)" : "var(--foreground)",
              fontFamily: "var(--font-sans)",
              fontSize: "var(--text-sm)",
              textAlign: "left",
              opacity: choice.disabled ? 0.5 : 1,
              cursor: choice.disabled ? "default" : "pointer",
            }}
          >
            <span style={{ flex: "none", width: 14, display: "flex" }}>
              {choice.selected ? <Check size={14} /> : choice.icon}
            </span>
            <span style={{ flex: 1, minWidth: 0 }}>{choice.label}</span>
            {choice.hint ? (
              <span
                style={{
                  flex: "none",
                  fontFamily: "var(--font-mono)",
                  fontSize: 11,
                  color: "var(--muted-foreground)",
                }}
              >
                {choice.hint}
              </span>
            ) : null}
          </button>
          {choice.action ? (
            <div style={{ flex: "none", padding: "2px 2px 2px 0" }}>{choice.action}</div>
          ) : null}
          </div>
        </Fragment>
      ))}
    </div>
  );
}

/**
 * An icon that opens a menu, for a control whose whole job is to offer a choice.
 *
 * Not a {@link SplitButton}: that shape has a usual thing it does, and the caret is the exception.
 * This one has no default at all — the `+` on the Projects header adds a folder or a worktree, and
 * picking one of those *is* the action.
 *
 * Orca reaches the same options through a dialog whose first step is the list
 * (`sidebar/AddRepoStartSteps.tsx`), and that is right for orca: each of its four ways in — browse,
 * clone, SSH host, create — opens its own multi-field form, so the choice belongs inside the thing
 * that will collect the fields. Here one option is a navigation and the other is a three-field
 * dialog, so a menu costs one click instead of two, and the design draws a caret beside the plus,
 * which is a menu's promise rather than a dialog's.
 */
export function MenuButton({
  label,
  choices,
  align,
  drop,
  variant = "ghost",
  size = "xs",
  style,
  floating = false,
  children,
}: {
  /** What the trigger is for, read out to a screen reader and shown on hover. */
  label: string;
  choices: MenuChoice[];
  /**
   * Draw the list `fixed` at the trigger rather than inside it, for a trigger that lives in a box
   * that scrolls. An absolute list is clipped by any ancestor with `overflow` — the rail's folder
   * rows are one (#471), and the session strip's `+` met the same wall before it moved outside its
   * own scroller. Taken once, as it opens: the list closes on the next press anywhere else.
   */
  floating?: boolean;
  align?: "start" | "end";
  /**
   * Which way the list opens.
   *
   * Down is right for every trigger this app had until the Chat tab's agent picker, which sits on
   * the composer at the bottom edge of the screen: a list that drops from there is a list whose
   * second and third rows are below the viewport. Measured at 1440×900 — one of three choices was
   * on screen. A prop rather than a measurement, because the two callers know which they are and a
   * flip that depends on scroll position is a menu that opens somewhere different each time.
   */
  drop?: "down" | "up";
  /**
   * How the trigger itself is drawn, for the callers whose trigger is not a quiet `+`.
   *
   * The header's hold counter is the reason these exist: it is the same control — a trigger with
   * no default action, dropping the same list — but it has to read as a count that wants
   * attention rather than as an affordance you go looking for. Adding the props here rather than
   * letting that screen draw its own popover is DESIGN.md's first rule: the kit gains what a
   * screen needs, and the screen composes it.
   */
  variant?: ButtonVariant;
  size?: ButtonSize;
  style?: React.CSSProperties;
  children: ReactNode;
}) {
  const [open, setOpen] = useState(false);
  const [anchor, setAnchor] = useState<DOMRect | undefined>(undefined);
  const box = useDismiss(open, setOpen);

  return (
    <div ref={box} style={{ position: "relative", display: "inline-flex" }}>
      <Button
        variant={variant}
        size={size}
        onClick={() => {
          if (floating) setAnchor(box.current?.getBoundingClientRect());
          setOpen((shown) => !shown);
        }}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={label}
        title={label}
        style={{ padding: "0 4px", color: "var(--muted-foreground)", ...style }}
      >
        {children}
      </Button>

      {open ? (
        <MenuList
          label={label}
          choices={choices}
          align={align}
          drop={drop}
          anchor={floating ? anchor : undefined}
          onDone={() => setOpen(false)}
        />
      ) : null}
    </div>
  );
}

type BadgeVariant = "default" | "secondary" | "outline" | "success" | "warning" | "info" | "error";

export function Badge({
  variant = "secondary",
  children,
  style,
}: {
  variant?: BadgeVariant;
  children: ReactNode;
  style?: React.CSSProperties;
}) {
  return (
    <span className={`pc-badge pc-badge--${variant}`} style={style}>
      {children}
    </span>
  );
}

export type CalloutTone = "neutral" | "info";

/**
 * A bordered block that carries a setting and the sentence explaining it.
 *
 * **The tone is the point.** A control whose two states produce genuinely different outcomes should
 * not look identical in both — reading the hint is the only way to tell, and nobody reads a hint
 * they have seen twenty times. `info` marks the state that departs from the usual one, so the
 * difference registers before the words do.
 *
 * Lives here rather than in the screen because a colour decision in a screen is the one thing
 * DESIGN.md rule 1 forbids. It consumes `--info`, which the dark theme redefines, so neither this
 * nor its caller ever branches on the theme.
 */
export function Callout({
  tone = "neutral",
  children,
}: {
  tone?: CalloutTone;
  children: ReactNode;
}) {
  const info = tone === "info";

  return (
    <div
      style={{
        display: "flex",
        alignItems: "flex-start",
        gap: 8,
        padding: 10,
        border: `1px solid ${info ? "var(--info)" : "var(--border)"}`,
        borderRadius: "var(--radius-lg)",
        // A tint of the same hue rather than a second colour: the border already names the state,
        // and a filled block would compete with the primary action directly below it.
        background: info
          ? "color-mix(in srgb, var(--info) 8%, var(--card))"
          : "var(--muted)",
      }}
    >
      {children}
    </div>
  );
}

export function Field({
  label,
  hint,
  children,
}: {
  label: string;
  hint?: string;
  children: ReactNode;
}) {
  return (
    <label style={{ display: "flex", flexDirection: "column", gap: 5 }}>
      <span
        style={{
          fontSize: "var(--text-xs)",
          fontWeight: 500,
          color: "var(--muted-foreground)",
        }}
      >
        {label}
      </span>
      {children}
      {hint ? (
        <span style={{ fontSize: 11, color: "var(--muted-foreground)", lineHeight: 1.45 }}>
          {hint}
        </span>
      ) : null}
    </label>
  );
}

export function Select({ mono, ...rest }: SelectHTMLAttributes<HTMLSelectElement> & { mono?: boolean }) {
  return (
    <select
      {...rest}
      className="pc-native-select"
      style={{
        height: 36,
        border: "1px solid var(--input)",
        borderRadius: "var(--radius-md)",
        background: "var(--background)",
        color: "var(--foreground)",
        fontFamily: mono ? "var(--font-mono)" : "var(--font-sans)",
        fontSize: "var(--text-sm)",
        padding: "0 8px",
        ...rest.style,
      }}
    />
  );
}

export function Dialog({
  title,
  onClose,
  children,
  footer,
  width = 520,
}: {
  title: string;
  onClose: () => void;
  children: ReactNode;
  footer: ReactNode;
  width?: number;
}) {
  return (
    <div
      className="pc-overlay"
      style={{ display: "flex", alignItems: "center", justifyContent: "center", zIndex: 60 }}
      // Clicking the scrim closes; clicking the panel must not. Dialogs here are short forms,
      // so a stray outside click losing three fields is an acceptable trade the design already makes.
      onClick={onClose}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-label={title}
        onClick={(event) => event.stopPropagation()}
        style={{
          width,
          // A phone is narrower than any dialog: the 16px gutter stays on both sides.
          maxWidth: "calc(100vw - 32px)",
          maxHeight: "88vh",
          overflow: "auto",
          display: "flex",
          flexDirection: "column",
          gap: 16,
          padding: 20,
          border: "1px solid var(--border)",
          borderRadius: "var(--radius-lg)",
          background: "var(--card)",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <h2 style={{ margin: 0, fontSize: "var(--text-base)", fontWeight: 600 }}>{title}</h2>
          <div style={{ flex: 1 }} />
          <Button
            variant="ghost"
            size="icon-xs"
            onClick={onClose}
            aria-label={t("dialog.close")}
            style={{ color: "var(--muted-foreground)" }}
          >
            ✕
          </Button>
        </div>
        {children}
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: 8,
            paddingTop: 12,
            borderTop: "1px solid var(--border)",
          }}
        >
          {footer}
        </div>
      </div>
    </div>
  );
}

export function Loading() {
  return (
    <div className="pc-empty">
      {t("state.loading")}
    </div>
  );
}

export function ErrorState({ detail, onRetry }: { detail: string; onRetry?: () => void }) {
  return (
    <div className="pc-empty" style={{ margin: 16 }}>
      <div className="pc-empty__header">
        <span className="pc-empty__title">{t("state.error")}</span>
        <span className="pc-empty__desc">{detail}</span>
      </div>
      {onRetry ? (
        <Button variant="outline" size="sm" onClick={onRetry}>
          {t("state.retry")}
        </Button>
      ) : null}
    </div>
  );
}

export function Empty({ messageKey, action }: { messageKey: TranslationKey; action?: ReactNode }) {
  return (
    <div className="pc-empty" style={{ margin: 16 }}>
      <div className="pc-empty__header">
        <span className="pc-empty__desc">{t(messageKey)}</span>
      </div>
      {action}
    </div>
  );
}

/**
 * An on/off control over the vendored `.pc-switch`: a button with `role="switch"`, so a screen
 * reader says on or off and Space and Enter toggle it like any button (#564).
 *
 * `blocked` is `aria-disabled` and not the DOM attribute, for the reason {@link MenuChoice} gives:
 * the switch stays in the tab order with the sentence that says why (`describedBy`), and pressing
 * it does nothing. `busy` is a change on its way to the server: the switch shows what is stored
 * until the next read says otherwise.
 */
export function Switch({
  on,
  label,
  onChange,
  blocked = false,
  busy = false,
  describedBy,
}: {
  on: boolean;
  label: string;
  onChange: (on: boolean) => void;
  blocked?: boolean;
  busy?: boolean;
  describedBy?: string;
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={on}
      aria-label={label}
      aria-disabled={blocked || undefined}
      aria-busy={busy || undefined}
      aria-describedby={describedBy}
      className="pc-switch"
      data-state={on ? "checked" : "unchecked"}
      onClick={() => {
        if (!blocked && !busy) onChange(!on);
      }}
      style={{
        opacity: blocked ? 0.5 : 1,
        cursor: blocked ? "not-allowed" : busy ? "progress" : "pointer",
      }}
    >
      <span className="pc-switch__thumb" />
    </button>
  );
}

/**
 * One of a few, each with the line that says what it means (#578): native radios in a bordered list,
 * so Tab reaches the group once and the arrows move the choice. The design system has no radio, so
 * its look is `.aio-choice` in `app.css`. `busy` is a change on its way to the server: picking
 * another does nothing until it is in.
 */
export function Choices<T extends string>({
  name,
  label,
  options,
  value,
  onChange,
  busy = false,
}: {
  name: string;
  label: string;
  options: readonly { value: T; title: string; detail: string }[];
  value: T;
  onChange: (value: T) => void;
  busy?: boolean;
}) {
  return (
    <div
      role="radiogroup"
      aria-label={label}
      aria-busy={busy || undefined}
      style={{
        display: "flex",
        flexDirection: "column",
        border: "1px solid var(--border)",
        borderRadius: "var(--radius-xl)",
        overflow: "hidden",
      }}
    >
      {options.map((option, index) => (
        <label
          key={option.value}
          className="aio-choice"
          style={{
            display: "flex",
            alignItems: "baseline",
            gap: 10,
            padding: "10px 14px",
            borderTop: index === 0 ? 0 : "1px solid var(--border)",
            cursor: busy ? "progress" : "pointer",
          }}
        >
          <input
            type="radio"
            name={name}
            value={option.value}
            checked={option.value === value}
            onChange={() => {
              if (!busy) onChange(option.value);
            }}
            style={{ margin: 0, flex: "none", transform: "translateY(2px)" }}
          />
          <span style={{ display: "flex", flexDirection: "column", gap: 2, minWidth: 0 }}>
            <span style={{ fontFamily: "var(--font-mono)", fontSize: "var(--text-sm)" }}>{option.title}</span>
            <span style={{ fontSize: "var(--text-xs)", color: "var(--muted-foreground)", lineHeight: 1.45 }}>
              {option.detail}
            </span>
          </span>
        </label>
      ))}
    </div>
  );
}
