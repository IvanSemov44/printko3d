import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, getProducts, getStoreSettings } from './client';

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

  describe('getProducts', () => {
    const item = {
      id: '1',
      name: 'Dragon',
      slug: 'dragon',
      shortDescription: '',
      fulfilmentType: 'MadeToOrder',
      isAvailable: true,
      primaryImage: null,
    };

    it('sends the category, page and page size', async () => {
      stubApi(200, { items: [], page: 2, pageSize: 12, totalCount: 0, totalPages: 0 });

      await getProducts({ category: 'puzzles', page: 2, pageSize: 12 });

      expect(fetch).toHaveBeenLastCalledWith(
        'http://api.test/api/v1/products?category=puzzles&page=2&pageSize=12',
        expect.anything(),
      );
    });

    it('turns integers sent as strings into numbers', async () => {
      stubApi(200, {
        items: [{ ...item, minPrice: '1250', leadTimeDays: '5' }],
        page: '1',
        pageSize: '12',
        totalCount: '1',
        totalPages: '1',
      });

      await expect(getProducts({ category: 'a', page: 1, pageSize: 12 })).resolves.toEqual({
        items: [{ ...item, minPrice: 1250, leadTimeDays: 5 }],
        page: 1,
        pageSize: 12,
        totalCount: 1,
        totalPages: 1,
      });
    });

    it('works out the page count when the answer has none, and keeps a missing lead time', async () => {
      stubApi(200, {
        items: [{ ...item, minPrice: 100, leadTimeDays: null }],
        page: 1,
        pageSize: 12,
        totalCount: 25,
      });

      const page = await getProducts({ category: 'a', page: 1, pageSize: 12 });

      expect(page.totalPages).toBe(3);
      expect(page.items[0]?.leadTimeDays).toBeNull();
    });

    it('fails on a price that is not a whole number', async () => {
      stubApi(200, {
        items: [{ ...item, minPrice: '12.5', leadTimeDays: null }],
        page: 1,
        pageSize: 12,
        totalCount: 1,
        totalPages: 1,
      });

      await expect(getProducts({ category: 'a', page: 1, pageSize: 12 })).rejects.toBeInstanceOf(
        RangeError,
      );
    });

    it('throws ApiError with the code on a rejected parameter', async () => {
      stubApi(400, { code: 'validation.failed' });

      await expect(getProducts({ category: 'a', page: 1, pageSize: 12 })).rejects.toMatchObject({
        status: 400,
        code: 'validation.failed',
      });
    });
  });
});
