/** The cart is one query. The server answers every change with the whole cart, and that answer replaces the cache. */
export const CART_KEY = ['cart'] as const;

/** Changes to one line share a key, so the shop can tell that a line is busy and send one request at a time for it. */
export function lineMutationKey(variantId: string) {
  return ['cart', 'line', variantId] as const;
}

/**
 * Every change to the cart waits for the one before it, whichever line it is for. The server answers with the whole
 * cart, so two answers that arrive in the wrong order would put an older cart in the cache.
 */
export const CART_MUTATION_SCOPE = { id: 'cart' } as const;

export const CLEAR_MUTATION_KEY = ['cart', 'clear'] as const;
