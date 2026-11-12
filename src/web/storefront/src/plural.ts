const FALLBACK_LOCALE = 'bg';

/** The plural form of a count in the given language: "one" or "other" are the two forms the shop has texts for. */
export function pluralCategory(locale: string, count: number): 'one' | 'other' {
  let rules: Intl.PluralRules;
  try {
    rules = new Intl.PluralRules(locale);
  } catch (error) {
    if (!(error instanceof RangeError)) {
      throw error;
    }
    rules = new Intl.PluralRules(FALLBACK_LOCALE);
  }
  return rules.select(count) === 'one' ? 'one' : 'other';
}
