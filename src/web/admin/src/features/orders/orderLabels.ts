import { ORDER_STATUSES, type OrderStatus } from '@storefront/shared';
import { t, type MessageKey } from '../../i18n';

/** The words for the statuses of an order, one key each. */
export const ORDER_STATUS_KEYS: Record<OrderStatus, MessageKey> = {
  New: 'orders.status.New',
  Paid: 'orders.status.Paid',
  InProduction: 'orders.status.InProduction',
  ReadyToShip: 'orders.status.ReadyToShip',
  Shipped: 'orders.status.Shipped',
  Completed: 'orders.status.Completed',
  Cancelled: 'orders.status.Cancelled',
};

/** The button that takes the order to this status. Only the four production steps have one. */
export const ORDER_STEP_KEYS: Partial<Record<OrderStatus, MessageKey>> = {
  InProduction: 'orders.step.InProduction',
  ReadyToShip: 'orders.step.ReadyToShip',
  Shipped: 'orders.step.Shipped',
  Completed: 'orders.step.Completed',
};

/** How the buyer sees the number: `PR-` and the eight digits. */
export function orderNumberText(number: number): string {
  return `PR-${String(number)}`;
}

/** The date and the time of an order, in the language of the shop. */
export function orderDateText(iso: string, locale: string): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'short' }).format(
    new Date(iso),
  );
}

/** The word for a status the API sent as text; a status that is not known is shown as it came. */
export function orderStatusText(status: string): string {
  const known = ORDER_STATUSES.find((item) => item === status);
  return known ? t(ORDER_STATUS_KEYS[known]) : status;
}
