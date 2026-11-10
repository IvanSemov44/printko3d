import type { AdminFulfilmentType, ProductStatus } from '@storefront/shared';
import type { MessageKey } from '../../i18n';

export const PRODUCT_STATUSES: readonly ProductStatus[] = ['Draft', 'Published', 'Archived'];

/** The message of each status. A lookup, so every status has a text and the compiler checks it. */
export const STATUS_KEYS = {
  Draft: 'products.status.Draft',
  Published: 'products.status.Published',
  Archived: 'products.status.Archived',
} as const satisfies Record<ProductStatus, MessageKey>;

export const FULFILMENT_KEYS = {
  InStock: 'products.fulfilment.InStock',
  MadeToOrder: 'products.fulfilment.MadeToOrder',
} as const satisfies Record<AdminFulfilmentType, MessageKey>;

export const STATUS_HINT_KEYS = {
  Draft: 'products.statusSection.hint.Draft',
  Published: 'products.statusSection.hint.Published',
  Archived: 'products.statusSection.hint.Archived',
} as const satisfies Record<ProductStatus, MessageKey>;

export const FULFILMENT_TYPES: readonly AdminFulfilmentType[] = ['InStock', 'MadeToOrder'];
