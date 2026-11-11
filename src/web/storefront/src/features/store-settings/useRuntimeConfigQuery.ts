import { loadRuntimeConfig } from '@storefront/shared';
import { useQuery } from '@tanstack/react-query';

export const runtimeConfigQueryKey = ['runtime-config'] as const;

/** /config.json, loaded once. The provider waits for it, so below the provider the data is always there. */
export function useRuntimeConfigQuery() {
  return useQuery({
    queryKey: runtimeConfigQueryKey,
    queryFn: ({ signal }) => loadRuntimeConfig(signal),
    staleTime: Infinity,
  });
}

/** The public address of the shop for canonical and sharing addresses; the address of the page when none is set. */
export function useSiteUrl(): string {
  const { data } = useRuntimeConfigQuery();
  return data?.siteUrl ?? window.location.origin;
}
