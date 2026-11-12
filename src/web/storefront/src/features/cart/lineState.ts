import type { CartLineState } from '@storefront/shared';
import type { MessageKey } from '../../i18n';
import { MAX_LINE_QUANTITY } from './limits';

/** The note under a line that cannot be bought as it is; `null` for a normal line. Rule 21 widens this. */
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

/** The stepper adds pieces only to a line that can be bought, and never past the limit. */
export function canIncrease(state: CartLineState, quantity: number): boolean {
  return state === 'Available' && quantity < MAX_LINE_QUANTITY;
}
