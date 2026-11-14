import { ORDER_STATUSES } from '@storefront/shared';
import type { MessageKey } from '../../i18n';

/** The steps of an order that goes well, in order. `Cancelled` is not a step: it ends the order. */
export const ORDER_STEPS = ORDER_STATUSES.filter((status) => status !== 'Cancelled');

const STATUS_TEXTS: Record<(typeof ORDER_STATUSES)[number], MessageKey> = {
  New: 'myOrder.status.New',
  Paid: 'myOrder.status.Paid',
  InProduction: 'myOrder.status.InProduction',
  ReadyToShip: 'myOrder.status.ReadyToShip',
  Shipped: 'myOrder.status.Shipped',
  Completed: 'myOrder.status.Completed',
  Cancelled: 'myOrder.status.Cancelled',
};

function isKnownStatus(status: string): status is keyof typeof STATUS_TEXTS {
  return Object.hasOwn(STATUS_TEXTS, status);
}

/** The text of a status. A status this page does not know (a newer server) gets a neutral text, never the raw word. */
export function statusText(status: string): MessageKey {
  return isKnownStatus(status) ? STATUS_TEXTS[status] : 'myOrder.status.unknown';
}

/** The place of a status among the steps, or -1 for a cancelled order and for a status that is not known. */
export function stepIndex(status: string): number {
  return ORDER_STEPS.findIndex((step) => step === status);
}
