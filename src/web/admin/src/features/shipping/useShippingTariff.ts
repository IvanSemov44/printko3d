import { getShippingTariff, saveShippingTariff, type ShippingTariffStep } from '@storefront/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

// The first part is "admin", so signing out removes the tariff with everything else loaded for the administrator.
const TARIFF_KEY = ['admin', 'shipping-tariff'] as const;

/** The tariff. No refetch in the background: the form keeps the steps it was loaded with. */
export function useShippingTariff() {
  return useQuery({
    queryKey: TARIFF_KEY,
    queryFn: ({ signal }) => getShippingTariff(signal),
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
}

/** After a save the answer, sorted by the server, replaces the copy in the cache. */
export function useSaveShippingTariff() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (steps: readonly ShippingTariffStep[]) => saveShippingTariff(steps),
    onSuccess: (saved) => {
      queryClient.setQueryData(TARIFF_KEY, saved);
    },
  });
}
