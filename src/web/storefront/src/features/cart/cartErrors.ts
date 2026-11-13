import { ApiError } from '@storefront/shared';
import type { MessageKey } from '../../i18n';

/**
 * The text for a failed cart change. The codes are the ones of the server (decision 0027); anything else, a lost
 * connection included, gets the general text and never shows what the server said.
 */
export function cartErrorKey(error: unknown): MessageKey {
  if (error instanceof ApiError) {
    switch (error.code) {
      case 'cart.too_many_items':
        return 'cart.error.tooManyItems';
      case 'cart.quantity_too_high':
        return 'cart.error.quantityTooHigh';
      case 'cart.quantity_exceeds_stock':
        return 'cart.error.quantityExceedsStock';
      case 'cart.item_not_found':
        return 'cart.error.itemNotFound';
      case 'variant.not_found':
        return 'cart.error.variantNotFound';
    }
  }
  return 'cart.error.generic';
}
