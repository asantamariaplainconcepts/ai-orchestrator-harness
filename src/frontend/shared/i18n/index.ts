import { en, type TranslationKey } from "./en";

/**
 * Lookup is typed: an unknown key is a compile error, so copy cannot drift from the catalogue.
 * A second locale would swap the catalogue here — no call site changes.
 */
export function t(key: TranslationKey, values?: Record<string, string>): string {
  const copy = en[key];

  if (!values) {
    return copy;
  }

  // Placeholders rather than concatenation: word order differs between languages, and a sentence
  // assembled from fragments at the call site cannot be reordered by a translator.
  return copy.replace(/\{(\w+)\}/g, (whole, name: string) => values[name] ?? whole);
}

/**
 * Counted nouns, chosen by the count rather than concatenated. "1 steps" is the kind of thing
 * that reads as unfinished software, and it only ever appears once real data arrives.
 */
export function tCount(count: number, one: string, other: string): string {
  return `${count} ${count === 1 ? one : other}`;
}

export type { TranslationKey };
