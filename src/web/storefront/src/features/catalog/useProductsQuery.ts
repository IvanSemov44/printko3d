import { getProducts } from '@storefront/shared';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { PAGE_SIZE } from './pageParams';

/** One page of a category's products. The previous page stays in `data` while the next one loads. */
export function useProductsQuery(category: string, page: number) {
  return useQuery({
    queryKey: ['products', category, page, PAGE_SIZE],
    queryFn: ({ signal }) => getProducts({ category, page, pageSize: PAGE_SIZE }, signal),
    placeholderData: keepPreviousData,
  });
}
