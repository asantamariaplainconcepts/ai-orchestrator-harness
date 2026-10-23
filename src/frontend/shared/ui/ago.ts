/**
 * How long ago an ISO timestamp was, as a short string.
 *
 * Three copies lived in three feature files because lifting it mid-wave
 * would have edited another ticket's file (AGENTS.md rule 4). The wave
 * is over; the three are one again.
 */
export function ago(iso: string): string {
  const seconds = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 1000));
  if (seconds < 60) return `${seconds}s`;
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m`;
  if (seconds < 86_400) return `${Math.floor(seconds / 3600)}h`;
  return `${Math.floor(seconds / 86_400)}d`;
}
