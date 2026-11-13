import type { FulfilmentType } from '@storefront/shared';
import type { Translate } from '../i18n';
import { formatLeadTime } from './leadTime';

interface AvailabilityInput {
  fulfilmentType: FulfilmentType;
  isAvailable: boolean;
  leadTimeDays: number | null;
}

export type AvailabilityKind = 'inStock' | 'soldOut' | 'madeToOrder';

/** The one rule for availability: a made-to-order item is never "sold out": it is made after the order. */
export function availabilityKind({
  fulfilmentType,
  isAvailable,
}: Pick<AvailabilityInput, 'fulfilmentType' | 'isAvailable'>): AvailabilityKind {
  if (fulfilmentType === 'MadeToOrder') {
    return 'madeToOrder';
  }
  return isAvailable ? 'inStock' : 'soldOut';
}

/** "In stock", "Sold out" or "Made to order" (with the lead time in days when it is set). */
export function describeAvailability(
  t: Translate,
  locale: string,
  input: AvailabilityInput,
): string {
  switch (availabilityKind(input)) {
    case 'madeToOrder': {
      const label = t('catalog.madeToOrder');
      return input.leadTimeDays === null
        ? label
        : `${label} · ${formatLeadTime(locale, input.leadTimeDays)}`;
    }
    case 'inStock':
      return t('catalog.inStock');
    case 'soldOut':
      return t('catalog.soldOut');
  }
}
