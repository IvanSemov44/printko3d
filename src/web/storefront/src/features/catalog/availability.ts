import type { FulfilmentType } from '@storefront/shared';
import type { Translate } from '../../i18n';
import { formatLeadTime } from './leadTime';

interface AvailabilityInput {
  fulfilmentType: FulfilmentType;
  isAvailable: boolean;
  leadTimeDays: number | null;
}

/**
 * "In stock", "Sold out" or "Made to order" (with the lead time in days when it is set).
 * A made-to-order item is never "sold out": it is made after the order.
 */
export function describeAvailability(
  t: Translate,
  locale: string,
  { fulfilmentType, isAvailable, leadTimeDays }: AvailabilityInput,
): string {
  if (fulfilmentType === 'MadeToOrder') {
    const label = t('catalog.madeToOrder');
    return leadTimeDays === null ? label : `${label} · ${formatLeadTime(locale, leadTimeDays)}`;
  }
  return isAvailable ? t('catalog.inStock') : t('catalog.soldOut');
}
