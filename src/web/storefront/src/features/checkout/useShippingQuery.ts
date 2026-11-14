import { getShippingOptions } from '@storefront/shared';
import { useQuery } from '@tanstack/react-query';
import { CART_KEY } from '../../components/cartKeys';

/** Under the key of the cart, so whatever refreshes the cart after a refusal prices the delivery again too. */
export const SHIPPING_KEY = [...CART_KEY, 'shipping'] as const;

/**
 * What delivery costs for the cart (decision 0034). It is asked for only when there is something to deliver, and it is
 * read again every time the checkout opens, because the price depends on the weight of the cart and on the tariff.
 */
export function useShippingQuery(isEnabled: boolean) {
  return useQuery({
    queryKey: SHIPPING_KEY,
    queryFn: ({ signal }) => getShippingOptions(signal),
    enabled: isEnabled,
  });
}
