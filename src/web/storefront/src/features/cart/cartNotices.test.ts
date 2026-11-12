import type { Cart } from '@storefront/shared';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { findNotices, loadSeen, saveSeen, snapshotOf } from './cartNotices';
import { cartFixture, cartLineFixture } from '../../testing/renderApp';

function cartOf(...overrides: Record<string, unknown>[]): Cart {
  return cartFixture(overrides.map((o) => cartLineFixture(o))) as unknown as Cart;
}

describe('findNotices', () => {
  it('says nothing about a line the buyer has not seen before', () => {
    expect(findNotices({}, cartOf({ variantId: 'a' }))).toEqual([]);
  });

  it('says nothing when price and state are the same', () => {
    const cart = cartOf({ variantId: 'a' });
    expect(findNotices(snapshotOf(cart), cart)).toEqual([]);
  });

  it('notices a changed price, with the old and the new one', () => {
    const before = snapshotOf(cartOf({ variantId: 'a', unitPrice: 1000 }));

    const notices = findNotices(before, cartOf({ variantId: 'a', unitPrice: 1200 }));

    expect(notices).toEqual([
      {
        id: 'a:price:1200',
        kind: 'price',
        variantId: 'a',
        productName: 'Dragon',
        from: 1000,
        to: 1200,
      },
    ]);
  });

  it.each(['InsufficientStock', 'Inactive', 'Archived'] as const)(
    'notices a line that stopped being available: %s',
    (state) => {
      const before = snapshotOf(cartOf({ variantId: 'a' }));

      const notices = findNotices(before, cartOf({ variantId: 'a', state }));

      expect(notices).toEqual([
        { id: `a:state:${state}`, kind: 'state', variantId: 'a', productName: 'Dragon', state },
      ]);
    },
  );

  it('says nothing when a line becomes available again', () => {
    const before = snapshotOf(cartOf({ variantId: 'a', state: 'InsufficientStock' }));

    expect(findNotices(before, cartOf({ variantId: 'a' }))).toEqual([]);
  });

  it('says nothing when the state is the same bad one as before', () => {
    const cart = cartOf({ variantId: 'a', state: 'Archived' });
    expect(findNotices(snapshotOf(cart), cart)).toEqual([]);
  });

  it('can notice both a price and a state of the same line, and only its own line', () => {
    const before = snapshotOf(
      cartOf({ variantId: 'a', unitPrice: 1000 }, { variantId: 'b', unitPrice: 500 }),
    );

    const notices = findNotices(
      before,
      cartOf(
        { variantId: 'a', unitPrice: 900, state: 'InsufficientStock' },
        { variantId: 'b', unitPrice: 500 },
      ),
    );

    expect(notices.map((n) => n.id)).toEqual(['a:price:900', 'a:state:InsufficientStock']);
  });

  it('does not take the price from the quantity: a line of many pieces at the same price is the same', () => {
    const before = snapshotOf(cartOf({ variantId: 'a', quantity: 1, linePrice: 1250 }));
    const cart = cartOf({ variantId: 'a', quantity: 4, linePrice: 5000 });

    expect(findNotices(before, cart)).toEqual([]);
  });
});

describe('what was seen last, kept for the tab', () => {
  afterEach(() => {
    sessionStorage.clear();
  });

  it('is nothing before anything was saved', () => {
    expect(loadSeen()).toBeNull();
  });

  it('keeps what was saved', () => {
    const snapshot = snapshotOf(cartOf({ variantId: 'a', unitPrice: 700 }));

    saveSeen(snapshot);

    expect(loadSeen()).toEqual(snapshot);
  });

  it.each([
    ['not json', '{oops'],
    ['not an object', '[1,2]'],
    ['a line without a price', JSON.stringify({ a: { state: 'Available' } })],
    ['a line with an unknown state', JSON.stringify({ a: { unitPrice: 1, state: 'Mystery' } })],
  ])('treats %s as nothing seen', (_name, stored) => {
    sessionStorage.setItem('printko-cart-seen', stored);

    expect(loadSeen()).toBeNull();
  });

  it('does not break when the storage cannot be used', () => {
    const blocked = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    try {
      expect(() => {
        saveSeen({});
      }).not.toThrow();
    } finally {
      blocked.mockRestore();
    }
  });
});
