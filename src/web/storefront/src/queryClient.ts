import { ApiError } from '@storefront/shared';
import { QueryClient } from '@tanstack/react-query';

const MAX_RETRIES = 2;

/** Client errors (4xx) will not change on a retry; network and server errors get a few more tries. */
export function shouldRetry(failureCount: number, error: Error): boolean {
  return !(error instanceof ApiError && error.status < 500) && failureCount < MAX_RETRIES;
}

export function createQueryClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: shouldRetry } } });
}
