import { getProduct } from '@storefront/shared';
import { useQuery } from '@tanstack/react-query';

/** One product by slug. */
export function useProductQuery(slug: string) {
  return useQuery({
    queryKey: ['product', slug],
    queryFn: ({ signal }) => getProduct(slug, signal),
  });
}
