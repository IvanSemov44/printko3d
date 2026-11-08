import { getTranslator } from '../../i18n';

const FALLBACK_LOCALE = 'bg';

function pluralCategory(locale: string, count: number): 'one' | 'other' {
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

/** "1 day" / "5 days" in the store's language, using the built-in plural rules. */
export function formatLeadTime(locale: string, days: number): string {
  const key =
    pluralCategory(locale, days) === 'one' ? 'catalog.leadTime.one' : 'catalog.leadTime.other';
  return getTranslator(locale)(key).replace('{count}', String(days));
}
