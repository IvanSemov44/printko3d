import {
  getAdminStoreSettings,
  updateStoreSettings,
  type AdminStoreSettings,
  type StoreSettingsInput,
} from '@storefront/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { STORE_MONEY_KEY } from '../products/useProducts';

// The first part is "admin", so signing out removes the settings with everything else loaded for the administrator.
const SETTINGS_KEY = ['admin', 'store-settings'] as const;

/** The settings with their version. No refetch in the background: the form keeps the version it was loaded with. */
export function useAdminStoreSettings() {
  return useQuery({
    queryKey: SETTINGS_KEY,
    queryFn: ({ signal }) => getAdminStoreSettings(signal),
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
}

/**
 * After a save the answer replaces the copy in the cache, and the currency and language the product forms use
 * are replaced too, so a price typed in the next form already follows the new currency.
 */
export function useSaveStoreSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (change: { input: StoreSettingsInput; version: number }) =>
      updateStoreSettings(change.input, change.version),
    onSuccess: (saved: AdminStoreSettings) => {
      queryClient.setQueryData(SETTINGS_KEY, saved);
      queryClient.setQueryData(STORE_MONEY_KEY, {
        currency: saved.defaultCurrency,
        locale: saved.defaultLocale,
      });
    },
  });
}
