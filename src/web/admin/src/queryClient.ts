import { QueryClient } from '@tanstack/react-query';

/** No automatic retries: a wrong password or a lost session will not change on a second try. */
export function createQueryClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}
