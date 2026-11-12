import {
  addCartItem,
  clearCart,
  removeCartItem,
  setCartItemQuantity,
  type Cart,
} from '@storefront/shared';
import { useIsMutating, useMutation, useQueryClient } from '@tanstack/react-query';
import { CART_KEY, CART_MUTATION_SCOPE, CLEAR_MUTATION_KEY, lineMutationKey } from './cartKeys';

/**
 * Keeps the cache equal to the server. A good answer is the new cart. A failed change may mean the cart is not what
 * the shop shows (a line is gone, a cookie expired), so the shop reads it again; the error still reaches the caller.
 */
function useCartCache() {
  const queryClient = useQueryClient();
  return {
    replace: (cart: Cart) => {
      queryClient.setQueryData(CART_KEY, cart);
    },
    reload: () => queryClient.invalidateQueries({ queryKey: CART_KEY }),
  };
}

export function useAddToCart(variantId: string) {
  const cache = useCartCache();
  return useMutation({
    mutationKey: lineMutationKey(variantId),
    mutationFn: (quantity: number) => addCartItem(variantId, quantity),
    scope: CART_MUTATION_SCOPE,
    onSuccess: cache.replace,
    onError: cache.reload,
  });
}

export function useSetCartQuantity(variantId: string) {
  const cache = useCartCache();
  return useMutation({
    mutationKey: lineMutationKey(variantId),
    mutationFn: (quantity: number) => setCartItemQuantity(variantId, quantity),
    scope: CART_MUTATION_SCOPE,
    onSuccess: cache.replace,
    onError: cache.reload,
  });
}

export function useRemoveFromCart(variantId: string) {
  const cache = useCartCache();
  return useMutation({
    mutationKey: lineMutationKey(variantId),
    mutationFn: () => removeCartItem(variantId),
    scope: CART_MUTATION_SCOPE,
    onSuccess: cache.replace,
    onError: cache.reload,
  });
}

export function useClearCart() {
  const cache = useCartCache();
  return useMutation({
    mutationKey: CLEAR_MUTATION_KEY,
    mutationFn: () => clearCart(),
    scope: CART_MUTATION_SCOPE,
    onSuccess: cache.replace,
    onError: cache.reload,
  });
}

/** True while a change to this line is on its way; the stepper and the buttons of the line wait for it (debt D55). */
export function useIsLineBusy(variantId: string): boolean {
  return useIsMutating({ mutationKey: lineMutationKey(variantId) }) > 0;
}
