import { getStoreMoneySettings } from '@storefront/shared';
import { useQuery } from '@tanstack/react-query';

/** The currency and locale of the shop. The settings screen replaces it after a save. */
export const STORE_MONEY_KEY = ['admin', 'store-money'] as const;

/** The currency and locale of the shop, for typing and showing prices. They hardly ever change. */
export function useStoreMoney() {
  return useQuery({
    queryKey: STORE_MONEY_KEY,
    queryFn: ({ signal }) => getStoreMoneySettings(signal),
    staleTime: Infinity,
  });
}
