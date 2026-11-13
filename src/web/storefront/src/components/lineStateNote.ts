import type { CartLineState } from '@storefront/shared';
import type { MessageKey } from '../i18n';

/** The note under a line that cannot be bought as it is; `null` for a normal line. Used by the cart and the checkout. */
export function lineStateNoteKey(state: CartLineState): MessageKey | null {
  switch (state) {
    case 'Available':
      return null;
    case 'InsufficientStock':
      return 'cart.state.insufficientStock';
    case 'Inactive':
    case 'Archived':
      return 'cart.state.unavailable';
  }
}
