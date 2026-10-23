/** How long, from two instants: "42s", "9m", "1h 38m". */
export function span(fromIso: string, toIso: string | null, now = Date.now()): string {
  const end = toIso === null ? now : Date.parse(toIso);
  const seconds = Math.max(0, Math.round((end - Date.parse(fromIso)) / 1000));
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  return minutes % 60 === 0 ? `${hours}h` : `${hours}h ${minutes % 60}m`;
}

/** Dollars to the cent, the unit the CLIs report in. */
export function dollars(cost: number): string {
  return `$${cost.toFixed(2)}`;
}

/** A count with its thousands marked: "38,210". */
export function count(value: number): string {
  return value.toLocaleString("en-US");
}

/** When, as the Journey reads it: "Tue 11:20" this week, the date before that. */
export function when(iso: string, now = Date.now()): string {
  const at = new Date(iso);
  const week = 6 * 86_400_000;
  return now - at.getTime() < week
    ? at.toLocaleString("en-GB", { weekday: "short", hour: "2-digit", minute: "2-digit" })
    : at.toLocaleString("en-GB", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
}
