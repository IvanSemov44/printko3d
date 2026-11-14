import { ApiError } from '@storefront/shared';
import type { MessageKey } from '../../i18n';

/**
 * The text for a refused search of an order (decision 0036). A wrong number, a wrong email and a number that was closed
 * after too many wrong emails look the same on purpose, so the page cannot tell them apart either.
 */
export function myOrderFailure(error: unknown): MessageKey {
  if (!(error instanceof ApiError)) {
    return 'myOrder.error.generic';
  }
  if (error.status === 429) {
    return 'myOrder.error.tooMany';
  }
  return error.code === 'order.not_found' ? 'myOrder.error.notFound' : 'myOrder.error.generic';
}

/** The eight digits of a number typed as `PR-12345678`, `pr 12345678` or just `12345678`, or null when it is not one. */
export function parseOrderNumber(typed: string): number | null {
  const digits = typed.trim().replace(/^pr[\s-]*/i, '');
  return /^\d{8}$/.test(digits) ? Number(digits) : null;
}

/** An email is only checked for its shape here; the server decides whether it is the one of the order. */
export function looksLikeEmail(typed: string): boolean {
  return /^\S+@\S+\.\S+$/.test(typed.trim());
}
