import { getCart } from '@storefront/shared';
import { useQuery } from '@tanstack/react-query';
import { CART_KEY } from './cartKeys';

/** The cart of this visitor. A new visitor gets an empty cart and no cookie (decision 0027). */
export function useCartQuery() {
  return useQuery({
    queryKey: CART_KEY,
    queryFn: ({ signal }) => getCart(signal),
  });
}
