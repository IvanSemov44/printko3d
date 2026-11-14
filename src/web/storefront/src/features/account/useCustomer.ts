import { getCurrentCustomer } from '@storefront/shared';
import { useQuery } from '@tanstack/react-query';

/** The signed-in buyer is one query: no buyer is `null`, not an error (decision 0039). */
export const CUSTOMER_KEY = ['customer'] as const;

export function useCustomer() {
  return useQuery({
    queryKey: CUSTOMER_KEY,
    queryFn: ({ signal }) => getCurrentCustomer(signal),
    staleTime: 60_000,
    retry: false,
  });
}
