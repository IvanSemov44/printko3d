/** The last moment to pay, written for the buyer in the language of the shop. An unreadable date gives an empty text. */
export function formatDeadline(iso: string, locale: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return '';
  }
  return new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'short' }).format(date);
}
