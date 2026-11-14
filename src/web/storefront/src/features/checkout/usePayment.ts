import { getPaymentState, startPayment, type Order, type PaymentState } from '@storefront/shared';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router';

/** How often the answer of the provider is asked for while it has not come, and for how many answers at most. */
export const POLL_MS = 2000;
export const MAX_POLLS = 15;

/**
 * An address inside the shop (the test provider). A second slash or a backslash after the first makes it another site
 * (`//host`, and `/\host` that a browser reads the same way), so those are not one.
 */
export function isOwnPath(address: string): boolean {
  return address.startsWith('/') && !address.startsWith('//') && !address.startsWith('/\\');
}

/**
 * Where the payment stands, asked again every few seconds while the provider has not answered. A buyer who comes back from
 * the provider before the message arrives sees "waiting", and the page changes by itself when the message is handled.
 */
export function usePaymentState(paymentId: string | null) {
  return useQuery<PaymentState>({
    queryKey: ['payment', paymentId],
    queryFn: ({ signal }) => getPaymentState(paymentId ?? '', signal),
    enabled: paymentId !== null,
    refetchInterval: (query) =>
      query.state.data === 'Pending' && query.state.dataUpdateCount < MAX_POLLS ? POLL_MS : false,
  });
}

/**
 * Opens the payment of the order and leads the buyer to where it is paid. The test provider is a page of the shop, so the
 * order goes along in the state of the route (there is no address to read it again, debt D65); a real provider is
 * another address, and only `https` is followed.
 */
export function useStartPayment(order: Order) {
  const navigate = useNavigate();
  return useMutation({
    mutationFn: () => startPayment(order.number),
    onSuccess: (session) => {
      if (isOwnPath(session.redirectUrl)) {
        void navigate(session.redirectUrl, { state: { order, paymentId: session.paymentId } });
      } else if (session.redirectUrl.startsWith('https://')) {
        window.location.assign(session.redirectUrl);
      }
    },
  });
}
