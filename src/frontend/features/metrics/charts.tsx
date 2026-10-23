/**
 * The Metrics screen's charts (#734): plain SVG over the design tokens, no chart library. As on the
 * Platform's Project Health board (`web/src/shared/ui/charts.tsx`), a chart uses the first two slots
 * of the chart ramp, which colour-blind readers tell apart, and every bar carries its value as text,
 * so no reading depends on colour alone.
 *
 * A bar is an SVG `rect` whose width is a percentage of its row, so it follows the screen to 390
 * wide without a resize listener; the label and the value stay HTML and wrap like the rest.
 */

/** One row per name, a bar per value, scaled to the largest value of the chart. */
export function BarRows({
  label,
  rows,
  colours,
}: {
  label: string;
  rows: readonly { name: string; values: readonly number[]; text: string }[];
  colours: readonly string[];
}) {
  const most = Math.max(0, ...rows.flatMap((row) => row.values));
  const height = 10;
  const gap = 3;
  return (
    <div role="img" aria-label={label} style={{ display: "flex", flexDirection: "column", gap: 8 }}>
      {rows.map((row) => (
        <div
          key={row.name}
          style={{
            display: "grid",
            gridTemplateColumns: "minmax(64px, 140px) minmax(0, 1fr) auto",
            gap: 8,
            alignItems: "center",
          }}
        >
          <span
            title={row.name}
            style={{
              fontFamily: "var(--font-mono)",
              fontSize: "var(--text-xs)",
              overflow: "hidden",
              textOverflow: "ellipsis",
              whiteSpace: "nowrap",
            }}
          >
            {row.name}
          </span>
          <svg width="100%" height={row.values.length * (height + gap)} aria-hidden="true">
            {row.values.map((value, index) => (
              <rect
                key={index}
                x={0}
                y={index * (height + gap)}
                width={`${most === 0 ? 0 : Math.max((value / most) * 100, value > 0 ? 1 : 0)}%`}
                height={height}
                rx={2}
                fill={colours[index] ?? colours[0]}
              />
            ))}
          </svg>
          <span
            style={{
              fontSize: "var(--text-xs)",
              color: "var(--muted-foreground)",
              fontVariantNumeric: "tabular-nums",
            }}
          >
            {row.text}
          </span>
        </div>
      ))}
    </div>
  );
}

/** One bar split into parts, each as wide as its share; the counts are written beside it. */
export function StackedBar({
  label,
  parts,
}: {
  label: string;
  parts: readonly { name: string; value: number; colour: string }[];
}) {
  const total = parts.reduce((sum, part) => sum + part.value, 0);
  let x = 0;
  return (
    <svg
      width="100%"
      height={12}
      role="img"
      aria-label={`${label}: ${parts
        .filter((part) => part.value > 0)
        .map((part) => `${part.name} ${part.value}`)
        .join(", ")}`}
    >
      {parts.map((part) => {
        const width = total === 0 ? 0 : (part.value / total) * 100;
        const at = x;
        x += width;
        return part.value === 0 ? null : (
          <rect
            key={part.name}
            x={`${at}%`}
            y={0}
            width={`${width}%`}
            height={12}
            fill={part.colour}
          />
        );
      })}
    </svg>
  );
}

export function Legend({ items }: { items: readonly { name: string; colour: string }[] }) {
  return (
    <ul
      style={{
        listStyle: "none",
        margin: 0,
        padding: 0,
        display: "flex",
        gap: "4px 16px",
        flexWrap: "wrap",
        fontSize: "var(--text-xs)",
        color: "var(--muted-foreground)",
      }}
    >
      {items.map((item) => (
        <li key={item.name} style={{ display: "flex", alignItems: "center", gap: 6 }}>
          <svg width={10} height={10} aria-hidden="true">
            <rect width={10} height={10} rx={2} fill={item.colour} />
          </svg>
          {item.name}
        </li>
      ))}
    </ul>
  );
}
