import { t } from "@/shared/i18n";

/**
 * How the Metrics screen writes a number (#734): unrounded where a person compares them, as
 * DESIGN.md#writing asks, and "none" where there is no measure, which is not zero.
 */

/** Seconds as the two largest units: "45s", "4m 12s", "3h 20m", "2d 4h". */
export function duration(seconds: number | null | undefined): string {
  if (seconds === null || seconds === undefined) return t("metrics.none");
  const whole = Math.round(seconds);
  const days = Math.floor(whole / 86_400);
  const hours = Math.floor((whole % 86_400) / 3_600);
  const minutes = Math.floor((whole % 3_600) / 60);
  const rest = whole % 60;
  if (days > 0) return `${days}d ${hours}h`;
  if (hours > 0) return `${hours}h ${minutes}m`;
  if (minutes > 0) return `${minutes}m ${rest}s`;
  return `${rest}s`;
}

/** Dollars to the cent. */
export function cost(usd: number | null | undefined): string {
  return usd === null || usd === undefined ? t("metrics.none") : `$${usd.toFixed(2)}`;
}

/** Tokens in thousands or millions, as the CLI's own totals read. */
export function tokens(count: number | null | undefined): string {
  if (count === null || count === undefined) return t("metrics.none");
  if (count >= 1_000_000) return `${(count / 1_000_000).toFixed(1)}M`;
  if (count >= 1_000) return `${Math.round(count / 1_000)}K`;
  return String(count);
}

const stamp = new Intl.DateTimeFormat(undefined, {
  day: "numeric",
  month: "short",
  hour: "2-digit",
  minute: "2-digit",
  hourCycle: "h23",
});

export function when(iso: string): string {
  return stamp.format(new Date(iso));
}
