import { getProducts } from '@storefront/shared';
import { useQuery } from '@tanstack/react-query';

export const PREVIEW_SIZE = 4;

/** The first few products of a category, for the home page. */
export function useCategoryPreviewQuery(category: string) {
  return useQuery({
    queryKey: ['products', category, 1, PREVIEW_SIZE],
    queryFn: ({ signal }) => getProducts({ category, page: 1, pageSize: PREVIEW_SIZE }, signal),
  });
}
