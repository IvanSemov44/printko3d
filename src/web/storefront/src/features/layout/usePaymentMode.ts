import { getPaymentMode } from '@storefront/shared';
import { useQuery } from '@tanstack/react-query';

/**
 * Whether the payment is a test one. It is read once and kept: it changes only when a provider is replaced. A failed
 * answer shows nothing, so the notice never blocks a page.
 */
export function usePaymentMode() {
  return useQuery({
    queryKey: ['payment', 'mode'],
    queryFn: ({ signal }) => getPaymentMode(signal),
    staleTime: Infinity,
  });
}
