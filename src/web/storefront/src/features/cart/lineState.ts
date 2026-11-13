import type { CartLine } from '@storefront/shared';
import { MAX_LINE_QUANTITY } from './limits';

/** The stepper adds pieces only to a line that can be bought, and never past the limit or the stock. */
export function canIncrease(
  line: Pick<CartLine, 'state' | 'quantity' | 'availableQuantity'>,
): boolean {
  const limit = Math.min(MAX_LINE_QUANTITY, line.availableQuantity ?? MAX_LINE_QUANTITY);
  return line.state === 'Available' && line.quantity < limit;
}
