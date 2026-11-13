import { placeOrder, type Cart, type PlaceOrderInput } from '@storefront/shared';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import { CART_KEY } from '../../components/cartKeys';
import { checkoutFailure } from './checkoutErrors';

const EMPTY_CART: Cart = { lines: [], itemCount: 0, total: 0 };

/**
 * Places the order. A placed order empties the cart on the server, so the cache says so before the buyer lands on the
 * confirmation, and the order goes along in the state of the route (there is no address to read it again, debt D65).
 * A refusal means the cart may not be what the page shows, so it is read again; if it is empty, the buyer is sent to it.
 */
export function usePlaceOrder() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  return useMutation({
    mutationFn: (input: PlaceOrderInput) => placeOrder(input),
    onSuccess: (order) => {
      queryClient.setQueryData(CART_KEY, EMPTY_CART);
      void navigate(`/order/${String(order.number)}`, { state: { order } });
    },
    onError: (error) => {
      void queryClient.invalidateQueries({ queryKey: CART_KEY });
      if (checkoutFailure(error).kind === 'empty') {
        void navigate('/cart', { replace: true });
      }
    },
  });
}
