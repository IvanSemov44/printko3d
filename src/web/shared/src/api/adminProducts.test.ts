import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  addVariant,
  createProduct,
  getAdminProduct,
  getAdminProducts,
  moveVariant,
  setProductStatus,
  setVariantActive,
  updateProduct,
  updateVariant,
  type ProductInput,
  type VariantInput,
} from './adminProducts';
import { ApiError } from './client';
import { UnexpectedResponseError } from './decode';

const VARIANT_DTO = {
  id: 'v1',
  sku: 'DRG-1',
  name: 'Син',
  price: '1500',
  compareAtPrice: null,
  stockQuantity: 5,
  weightGrams: 120,
  attributes: { color: 'blue' },
  isActive: true,
  sortOrder: 0,
};

const PRODUCT_DTO = {
  id: 'p1',
  categoryId: 'c1',
  name: 'Дракон',
  slug: 'drakon',
  shortDescription: 'Кратко',
  description: 'Дълго',
  fulfilmentType: 'InStock',
  leadTimeDays: null,
  status: 'Draft',
  seoTitle: null,
  seoDescription: null,
  version: '9',
  variants: [VARIANT_DTO],
};

const PRODUCT_INPUT: ProductInput = {
  categoryId: 'c1',
  name: 'Дракон',
  slug: 'drakon',
  shortDescription: 'Кратко',
  description: 'Дълго',
  fulfilmentType: 'InStock',
  leadTimeDays: null,
  seoTitle: null,
  seoDescription: null,
};

const VARIANT_INPUT: VariantInput = {
  sku: 'DRG-1',
  name: 'Син',
  price: 1500,
  compareAtPrice: null,
  stockQuantity: 5,
  weightGrams: 120,
  attributes: { color: 'blue' },
};

function stubFetch(response: Response) {
  const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(() =>
    Promise.resolve(response.clone()),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function bodyOf(init: RequestInit | undefined): unknown {
  return typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('admin products API', () => {
  it('asks for a page with the filters that are set, and reads the numbers', async () => {
    const fetchMock = stubFetch(
      Response.json({
        items: [
          {
            id: 'p1',
            name: 'Дракон',
            slug: 'drakon',
            categoryId: 'c1',
            categoryName: 'Фигури',
            status: 'Published',
            fulfilmentType: 'InStock',
            variantCount: 2,
            minPrice: '1500',
            totalStock: null,
            version: '4',
          },
        ],
        page: 2,
        pageSize: 20,
        totalCount: 41,
        totalPages: 3,
      }),
    );

    const page = await getAdminProducts({
      status: 'Published',
      search: 'дра',
      page: 2,
      pageSize: 20,
    });

    const [url] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe(
      `/api/v1/admin/products?status=Published&search=${encodeURIComponent('дра')}&page=2&pageSize=20`,
    );
    expect(page.items[0]).toMatchObject({ minPrice: 1500, totalStock: null, version: 4 });
    expect(page.totalPages).toBe(3);
  });

  it('leaves out the filters that are not set', async () => {
    const fetchMock = stubFetch(Response.json({ items: [], page: 1, pageSize: 20, totalCount: 0 }));

    const page = await getAdminProducts({ page: 1, pageSize: 20 });

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/admin/products?page=1&pageSize=20');
    expect(page.totalPages).toBe(0);
  });

  it('reads one product with its variants as real numbers', async () => {
    stubFetch(Response.json(PRODUCT_DTO));

    const product = await getAdminProduct('p1');

    expect(product.version).toBe(9);
    expect(product.variants[0]).toMatchObject({ price: 1500, stockQuantity: 5, isActive: true });
  });

  it('fails loudly on an answer of the wrong shape', async () => {
    stubFetch(Response.json({ ...PRODUCT_DTO, status: 'Deleted' }));

    await expect(getAdminProduct('p1')).rejects.toThrow(UnexpectedResponseError);
  });

  it('fails loudly when a number is missing', async () => {
    stubFetch(Response.json({ ...PRODUCT_DTO, version: undefined }));

    await expect(getAdminProduct('p1')).rejects.toThrow(UnexpectedResponseError);
  });

  it('turns an error answer into an ApiError with its code', async () => {
    stubFetch(Response.json({ code: 'product.not_found' }, { status: 404 }));

    const error = await getAdminProduct('p1').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 404, code: 'product.not_found' });
  });

  it('creates a product with the header that protects against cross-site requests', async () => {
    const fetchMock = stubFetch(Response.json(PRODUCT_DTO, { status: 201 }));

    await createProduct(PRODUCT_INPUT);

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/admin/products');
    expect(init?.method).toBe('POST');
    expect(init?.headers).toMatchObject({ 'X-Requested-With': 'XMLHttpRequest' });
    expect(bodyOf(init)).toEqual(PRODUCT_INPUT);
  });

  it('reports a taken slug as a 409 with its code', async () => {
    stubFetch(Response.json({ code: 'product.slug_taken' }, { status: 409 }));

    await expect(createProduct(PRODUCT_INPUT)).rejects.toMatchObject({
      status: 409,
      code: 'product.slug_taken',
    });
  });

  it('sends the version with an update, and the stock of the variants when it is given', async () => {
    const fetchMock = stubFetch(Response.json(PRODUCT_DTO));

    await updateProduct('p1', { ...PRODUCT_INPUT, variantStock: { v1: 3 } }, 9);

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/admin/products/p1');
    expect(init?.method).toBe('PUT');
    expect(bodyOf(init)).toEqual({ ...PRODUCT_INPUT, variantStock: { v1: 3 }, version: 9 });
  });

  it('changes the status with the version', async () => {
    const fetchMock = stubFetch(Response.json({ ...PRODUCT_DTO, status: 'Published' }));

    const product = await setProductStatus('p1', 'Published', 9);

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/admin/products/p1/status');
    expect(init?.method).toBe('PUT');
    expect(bodyOf(init)).toEqual({ status: 'Published', version: 9 });
    expect(product.status).toBe('Published');
  });

  it('adds a variant as active, at the given place', async () => {
    const fetchMock = stubFetch(Response.json(PRODUCT_DTO, { status: 201 }));

    await addVariant('p1', VARIANT_INPUT, 3);

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/admin/products/p1/variants');
    expect(init?.method).toBe('POST');
    expect(bodyOf(init)).toEqual({ ...VARIANT_INPUT, isActive: true, sortOrder: 3 });
  });

  it('updates, activates and moves a variant at its own addresses', async () => {
    const fetchMock = stubFetch(Response.json(PRODUCT_DTO));

    await updateVariant('p1', 'v1', VARIANT_INPUT);
    await setVariantActive('p1', 'v1', false);
    await moveVariant('p1', 'v1', 'Up');

    const calls = fetchMock.mock.calls.map(([url, init]) => [init?.method, url, bodyOf(init)]);
    expect(calls).toEqual([
      ['PUT', '/api/v1/admin/products/p1/variants/v1', VARIANT_INPUT],
      ['PUT', '/api/v1/admin/products/p1/variants/v1/active', { isActive: false }],
      ['POST', '/api/v1/admin/products/p1/variants/v1/move', { direction: 'Up' }],
    ]);
  });
});
