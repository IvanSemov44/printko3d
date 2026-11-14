import { ApiError } from '@storefront/shared';
import type { MessageKey } from '../../i18n';
import { CHECKOUT_FIELDS, type CheckoutField } from './checkoutValidation';

/** What the page does about a refused order. */
export type CheckoutFailure =
  /** A notice above the form. `toCart` adds a link to the cart, where the buyer can fix what the notice says. */
  | { kind: 'notice'; message: MessageKey; toCart: boolean }
  /** The cart is empty on the server: there is nothing to order, so the buyer goes to the cart. */
  | { kind: 'empty' }
  /** The server refused these fields; each is marked next to its input. */
  | { kind: 'fields'; fields: CheckoutField[] };

const GENERIC: CheckoutFailure = {
  kind: 'notice',
  message: 'checkout.error.generic',
  toCart: false,
};

/**
 * Sorts a failed order by the code of the server (decision 0032). Anything unknown, a lost connection included,
 * gets the general notice and never shows what the server said.
 */
export function checkoutFailure(error: unknown): CheckoutFailure {
  if (!(error instanceof ApiError)) {
    return GENERIC;
  }
  switch (error.code) {
    case 'order.total_changed':
      return { kind: 'notice', message: 'checkout.error.totalChanged', toCart: true };
    case 'order.cart_not_orderable':
      return { kind: 'notice', message: 'checkout.error.notOrderable', toCart: true };
    case 'order.stock_changed':
      return { kind: 'notice', message: 'checkout.error.stockChanged', toCart: true };
    case 'cart.changed':
      return { kind: 'notice', message: 'checkout.error.cartChanged', toCart: false };
    case 'order.cart_empty':
      return { kind: 'empty' };
    case 'validation.failed': {
      const fields = CHECKOUT_FIELDS.filter((field) => error.fields.includes(field));
      return fields.length > 0 ? { kind: 'fields', fields } : GENERIC;
    }
    default:
      return GENERIC;
  }
}
