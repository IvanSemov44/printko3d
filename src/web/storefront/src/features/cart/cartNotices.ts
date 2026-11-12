import type { Cart, CartLineState } from '@storefront/shared';

/** What the buyer saw of one line the last time: only what can change under their feet (decision 0027). */
export interface LineSnapshot {
  unitPrice: number;
  state: CartLineState;
}

/** By variant. The server keeps no "last seen" price, so the comparison is made here, in the tab. */
export type Snapshot = Record<string, LineSnapshot>;

export type CartNotice =
  | {
      id: string;
      kind: 'price';
      variantId: string;
      productName: string;
      /** In the smallest unit of the currency, as the server gave it. */
      from: number;
      to: number;
    }
  | {
      id: string;
      kind: 'state';
      variantId: string;
      productName: string;
      /** The state the line is in now; never `Available`. */
      state: Exclude<CartLineState, 'Available'>;
    };

const STORAGE_KEY = 'printko-cart-seen';
const STATES: readonly CartLineState[] = ['Available', 'InsufficientStock', 'Inactive', 'Archived'];

export function snapshotOf(cart: Cart): Snapshot {
  return Object.fromEntries(
    cart.lines.map((line) => [line.variantId, { unitPrice: line.unitPrice, state: line.state }]),
  );
}

/**
 * What changed for the buyer since `previous`: a different price, or a line that can no longer be bought as it was.
 * A line that was not there before, and a line that became available again, are not news. Both numbers come from
 * the server; nothing is worked out here.
 */
export function findNotices(previous: Snapshot, cart: Cart): CartNotice[] {
  const notices: CartNotice[] = [];
  for (const line of cart.lines) {
    const before = previous[line.variantId];
    if (!before) {
      continue;
    }
    if (before.unitPrice !== line.unitPrice) {
      notices.push({
        id: `${line.variantId}:price:${String(line.unitPrice)}`,
        kind: 'price',
        variantId: line.variantId,
        productName: line.productName,
        from: before.unitPrice,
        to: line.unitPrice,
      });
    }
    if (line.state !== 'Available' && line.state !== before.state) {
      notices.push({
        id: `${line.variantId}:state:${line.state}`,
        kind: 'state',
        variantId: line.variantId,
        productName: line.productName,
        state: line.state,
      });
    }
  }
  return notices;
}

function isSnapshot(value: unknown): value is Snapshot {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return false;
  }
  return Object.values(value).every((entry: unknown) => {
    if (typeof entry !== 'object' || entry === null) {
      return false;
    }
    const { unitPrice, state } = entry as Record<string, unknown>;
    return (
      typeof unitPrice === 'number' &&
      typeof state === 'string' &&
      STATES.includes(state as CartLineState)
    );
  });
}

/** Null when nothing was saved, or the storage is blocked or holds something else: then there is nothing to compare. */
export function loadSeen(): Snapshot | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (raw === null) {
      return null;
    }
    const parsed: unknown = JSON.parse(raw);
    return isSnapshot(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

/** The storage may be blocked (a private window, cleared site data): the notices are then only lost, never an error. */
export function saveSeen(snapshot: Snapshot): void {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(snapshot));
  } catch {
    // Nothing to do: the cart works without it.
  }
}
