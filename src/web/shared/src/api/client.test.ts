import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, getStoreSettings } from './client';

function stubApi(status: number, body: unknown) {
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string) =>
      Promise.resolve(
        url === '/config.json'
          ? Response.json({ apiUrl: 'http://api.test/' })
          : Response.json(body, { status }),
      ),
    ),
  );
}

describe('API client', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('reads JSON from the configured API address', async () => {
    stubApi(200, { storeName: 'Shop' });

    await expect(getStoreSettings()).resolves.toEqual({ storeName: 'Shop' });
    expect(fetch).toHaveBeenCalledWith('http://api.test/api/v1/store-settings', expect.anything());
  });

  it('throws ApiError with the status and the ProblemDetails code', async () => {
    stubApi(404, { code: 'store_settings.not_found' });

    await expect(getStoreSettings()).rejects.toMatchObject({
      status: 404,
      code: 'store_settings.not_found',
    });
    await expect(getStoreSettings()).rejects.toBeInstanceOf(ApiError);
  });

  it('leaves code empty when the error body has none', async () => {
    stubApi(500, 'oops');

    await expect(getStoreSettings()).rejects.toMatchObject({ status: 500, code: null });
  });
});
