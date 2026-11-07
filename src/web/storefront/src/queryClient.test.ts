import { ApiError } from '@storefront/shared';
import { describe, expect, it } from 'vitest';
import { shouldRetry } from './queryClient';

describe('shouldRetry', () => {
  it('does not retry client errors such as 404', () => {
    expect(shouldRetry(0, new ApiError(404, 'store_settings.not_found'))).toBe(false);
  });

  it('retries server and network errors a limited number of times', () => {
    expect(shouldRetry(0, new ApiError(503, null))).toBe(true);
    expect(shouldRetry(1, new TypeError('Failed to fetch'))).toBe(true);
    expect(shouldRetry(2, new TypeError('Failed to fetch'))).toBe(false);
  });
});
