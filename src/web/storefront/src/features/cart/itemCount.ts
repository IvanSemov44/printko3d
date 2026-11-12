import { getTranslator } from '../../i18n';
import { pluralCategory } from '../../plural';

/** The count shown on the badge: the exact number up to 99, then "99+". */
export function badgeText(count: number): string {
  return count > 99 ? '99+' : String(count);
}

/** The name of the cart button for a screen reader: "Cart, empty" or "Cart, 3 items" with the right plural form. */
export function cartButtonLabel(locale: string, count: number): string {
  const t = getTranslator(locale);
  if (count <= 0) {
    return t('cart.buttonEmpty');
  }
  const key =
    pluralCategory(locale, count) === 'one' ? 'cart.buttonCount.one' : 'cart.buttonCount.other';
  return t(key, { count });
}
