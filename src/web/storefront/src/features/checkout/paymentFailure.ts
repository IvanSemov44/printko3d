import { ApiError } from '@storefront/shared';
import type { MessageKey } from '../../i18n';

/**
 * The text for a payment that did not start, by the code of the server (decision 0035). Anything unknown, a lost
 * connection included, gets the general text and never shows what the server said.
 */
export function startPaymentFailure(error: unknown): MessageKey {
  if (error instanceof ApiError) {
    switch (error.code) {
      case 'payment.order_not_payable':
        return 'payment.error.notPayable';
      case 'payment.disabled':
        return 'payment.error.disabled';
      default:
        break;
    }
  }
  return 'payment.error.generic';
}

/** The text for a test payment that did not end. */
export function completeTestFailure(error: unknown): MessageKey {
  return error instanceof ApiError && error.code === 'payment.session_expired'
    ? 'payment.testPage.expired'
    : 'payment.testPage.error';
}
