import { getTranslator } from '../i18n';
import { pluralCategory } from '../plural';

/** "1 day" / "5 days" in the store's language, using the built-in plural rules. */
export function formatLeadTime(locale: string, days: number): string {
  const key =
    pluralCategory(locale, days) === 'one' ? 'catalog.leadTime.one' : 'catalog.leadTime.other';
  return getTranslator(locale)(key).replace('{count}', String(days));
}
