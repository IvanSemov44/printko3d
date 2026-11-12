/** The cart is one query. The server answers every change with the whole cart, and that answer replaces the cache. */
export const CART_KEY = ['cart'] as const;

/** Changes to one line share a key, so the shop can tell that a line is busy and send one request at a time for it. */
export function lineMutationKey(variantId: string) {
  return ['cart', 'line', variantId] as const;
}

export const CLEAR_MUTATION_KEY = ['cart', 'clear'] as const;
