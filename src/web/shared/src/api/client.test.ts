import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, getProduct, getProducts, getStoreSettings } from './client';

function stubApi(status: number, body: unknown, config: Record<string, unknown> = {}) {
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string) =>
      Promise.resolve(
        url === '/config.json'
          ? Response.json({ apiUrl: 'http://api.test/', ...config })
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

  describe('getProduct', () => {
    const variant = {
      id: 'v1',
      sku: 'DRG-S',
      name: 'Small',
      price: '1250',
      compareAtPrice: null,
      isAvailable: true,
      weightGrams: '40',
      attributes: { size: 'S' },
      sortOrder: '2',
    };
    const image = {
      id: 'i1',
      variantId: null,
      storageKey: 'demo/dragon 1.jpg',
      altText: 'A dragon',
      sortOrder: '1',
      isPrimary: true,
    };
    const product = {
      id: 'p1',
      name: 'Dragon',
      slug: 'dragon',
      shortDescription: 'Short',
      description: 'Long',
      fulfilmentType: 'MadeToOrder',
      leadTimeDays: '5',
      seoTitle: '',
      seoDescription: '',
      isAvailable: true,
      variants: [variant],
      images: [image],
    };

    it('asks for the product by its encoded slug', async () => {
      stubApi(200, product);

      await getProduct('a b');

      expect(fetch).toHaveBeenLastCalledWith(
        'http://api.test/api/v1/products/a%20b',
        expect.anything(),
      );
    });

    it('turns integers sent as strings into numbers', async () => {
      stubApi(200, product);

      const result = await getProduct('dragon');

      expect(result.leadTimeDays).toBe(5);
      expect(result.variants[0]).toMatchObject({ price: 1250, weightGrams: 40, sortOrder: 2 });
      expect(result.images[0]?.sortOrder).toBe(1);
    });

    it('keeps a missing lead time and a missing old price', async () => {
      stubApi(200, { ...product, leadTimeDays: null });

      const result = await getProduct('dragon');

      expect(result.leadTimeDays).toBeNull();
      expect(result.variants[0]?.compareAtPrice).toBeNull();
    });

    it('narrows the old price', async () => {
      stubApi(200, { ...product, variants: [{ ...variant, compareAtPrice: '1500' }] });

      expect((await getProduct('dragon')).variants[0]?.compareAtPrice).toBe(1500);
    });

    it('orders variants and images by sort order', async () => {
      stubApi(200, {
        ...product,
        variants: [variant, { ...variant, id: 'v0', sku: 'DRG-XS', sortOrder: 1 }],
        images: [image, { ...image, id: 'i0', sortOrder: 0 }],
      });

      const result = await getProduct('dragon');

      expect(result.variants.map((v) => v.sku)).toEqual(['DRG-XS', 'DRG-S']);
      expect(result.images.map((i) => i.id)).toEqual(['i0', 'i1']);
    });

    it('builds image addresses from the image base address in the config', async () => {
      stubApi(200, product, { imageBaseUrl: 'http://img.test/' });

      expect((await getProduct('dragon')).images[0]?.url).toBe(
        'http://img.test/demo/dragon%201.jpg',
      );
    });

    it('leaves the image address empty when the config has no image base address', async () => {
      stubApi(200, product);

      expect((await getProduct('dragon')).images[0]?.url).toBeNull();
    });

    it('fails on a price that is not a whole number', async () => {
      stubApi(200, { ...product, variants: [{ ...variant, price: '12.5' }] });

      await expect(getProduct('dragon')).rejects.toBeInstanceOf(RangeError);
    });

    it('throws ApiError with the code for an unknown product', async () => {
      stubApi(404, { code: 'product.not_found' });

      await expect(getProduct('nothing')).rejects.toMatchObject({
        status: 404,
        code: 'product.not_found',
      });
    });
  });
});
