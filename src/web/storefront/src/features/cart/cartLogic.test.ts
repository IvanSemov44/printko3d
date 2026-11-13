import { ApiError } from '@storefront/shared';
import { describe, expect, it } from 'vitest';
import { cartErrorKey } from './cartErrors';
import { badgeText, cartButtonLabel } from './itemCount';
import { canIncrease, lineStateNoteKey } from './lineState';

describe('badgeText', () => {
  it('shows the exact number up to 99 and "99+" above', () => {
    expect(badgeText(1)).toBe('1');
    expect(badgeText(99)).toBe('99');
    expect(badgeText(100)).toBe('99+');
  });
});

describe('cartButtonLabel', () => {
  it('says the cart is empty, and uses the right plural form otherwise', () => {
    expect(cartButtonLabel('bg', 0)).toBe('Количка, празна');
    expect(cartButtonLabel('bg', 1)).toBe('Количка, 1 артикул');
    expect(cartButtonLabel('bg', 3)).toBe('Количка, 3 артикула');
  });
});

describe('cartErrorKey', () => {
  it.each([
    ['cart.too_many_items', 'cart.error.tooManyItems'],
    ['cart.quantity_too_high', 'cart.error.quantityTooHigh'],
    ['cart.quantity_exceeds_stock', 'cart.error.quantityExceedsStock'],
    ['cart.item_not_found', 'cart.error.itemNotFound'],
    ['variant.not_found', 'cart.error.variantNotFound'],
  ])('maps the code %s to its own text', (code, key) => {
    expect(cartErrorKey(new ApiError(code === 'variant.not_found' ? 404 : 422, code))).toBe(key);
  });

  it('gives the general text for an unknown code, a missing code and a lost connection', () => {
    expect(cartErrorKey(new ApiError(500, 'something.internal'))).toBe('cart.error.generic');
    expect(cartErrorKey(new ApiError(503, null))).toBe('cart.error.generic');
    expect(cartErrorKey(new TypeError('Failed to fetch'))).toBe('cart.error.generic');
  });
});

describe('line state', () => {
  it('has no note for a line that can be bought and a note for the others', () => {
    expect(lineStateNoteKey('Available')).toBeNull();
    expect(lineStateNoteKey('InsufficientStock')).toBe('cart.state.insufficientStock');
    expect(lineStateNoteKey('Inactive')).toBe('cart.state.unavailable');
    expect(lineStateNoteKey('Archived')).toBe('cart.state.unavailable');
  });

  it('adds pieces only to a line that can be bought, and never past 20', () => {
    expect(canIncrease({ state: 'Available', quantity: 19, availableQuantity: null })).toBe(true);
    expect(canIncrease({ state: 'Available', quantity: 20, availableQuantity: null })).toBe(false);
    expect(canIncrease({ state: 'InsufficientStock', quantity: 1, availableQuantity: 5 })).toBe(
      false,
    );
    expect(canIncrease({ state: 'Inactive', quantity: 1, availableQuantity: 5 })).toBe(false);
  });

  it('stops at the stock when there are fewer than 20 pieces', () => {
    expect(canIncrease({ state: 'Available', quantity: 2, availableQuantity: 3 })).toBe(true);
    expect(canIncrease({ state: 'Available', quantity: 3, availableQuantity: 3 })).toBe(false);
  });

  it('does not stop a made-to-order line at the stock, only at 20', () => {
    expect(canIncrease({ state: 'Available', quantity: 10, availableQuantity: null })).toBe(true);
  });
});
