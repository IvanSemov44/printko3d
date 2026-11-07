import { getCategories } from '@storefront/shared';
import { useQuery } from '@tanstack/react-query';

export function useCategoriesQuery() {
  return useQuery({
    queryKey: ['categories'],
    queryFn: ({ signal }) => getCategories(signal),
    staleTime: 5 * 60 * 1000,
  });
}
