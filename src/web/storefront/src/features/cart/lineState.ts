import type { CartLine, CartLineState } from '@storefront/shared';
import type { MessageKey } from '../../i18n';
import { MAX_LINE_QUANTITY } from './limits';

/** The note under a line that cannot be bought as it is; `null` for a normal line. */
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

/** The stepper adds pieces only to a line that can be bought, and never past the limit or the stock. */
export function canIncrease(
  line: Pick<CartLine, 'state' | 'quantity' | 'availableQuantity'>,
): boolean {
  const limit = Math.min(MAX_LINE_QUANTITY, line.availableQuantity ?? MAX_LINE_QUANTITY);
  return line.state === 'Available' && line.quantity < limit;
}
