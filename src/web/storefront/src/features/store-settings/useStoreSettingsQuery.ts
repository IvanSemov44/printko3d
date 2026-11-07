import { getStoreSettings } from '@storefront/shared';
import { useQuery } from '@tanstack/react-query';

export const storeSettingsQueryKey = ['store-settings'] as const;

/** Loads the settings once; they do not change during a visit. */
export function useStoreSettingsQuery() {
  return useQuery({
    queryKey: storeSettingsQueryKey,
    queryFn: ({ signal }) => getStoreSettings(signal),
    staleTime: Infinity,
  });
}
