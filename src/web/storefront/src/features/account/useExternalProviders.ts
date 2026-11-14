import { getExternalProviders } from '@storefront/shared';
import { useQuery } from '@tanstack/react-query';

/**
 * The social providers that are turned on in this environment (decision 0039). While the answer is not known, and when it cannot be
 * had, there are none: the page then has no buttons, and the sign-in with a password still works.
 */
export function useExternalProviders(): readonly string[] {
  const { data } = useQuery({
    queryKey: ['external-providers'],
    queryFn: ({ signal }) => getExternalProviders(signal),
    staleTime: 10 * 60_000,
    retry: false,
  });
  return data ?? [];
}
