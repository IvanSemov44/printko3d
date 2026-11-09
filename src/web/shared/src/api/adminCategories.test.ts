import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  createCategory,
  getAdminCategories,
  moveCategory,
  setCategoryActive,
  updateCategory,
} from './adminCategories';
import { ApiError } from './client';

const DTO = {
  id: '11111111-1111-1111-1111-111111111111',
  parentId: null,
  name: 'Фигури',
  slug: 'figuri',
  description: null,
  sortOrder: '2',
  isActive: true,
  productCount: 4,
  version: '7',
};

const INPUT = {
  name: 'Фигури',
  slug: 'figuri',
  parentId: null,
  description: null,
  sortOrder: 2,
  isActive: true,
};

function stubFetch(response: Response) {
  const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(() =>
    Promise.resolve(response),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function bodyOf(init: RequestInit | undefined): unknown {
  return JSON.parse(typeof init?.body === 'string' ? init.body : 'null');
}

describe('admin categories API', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('reads the list from the panel address and turns the integers into numbers', async () => {
    const fetchMock = stubFetch(Response.json([DTO]));

    const list = await getAdminCategories();

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/admin/categories');
    expect(list).toEqual([{ ...DTO, sortOrder: 2, productCount: 4, version: 7 }]);
  });

  it('creates with POST and the header that protects against forged requests', async () => {
    const fetchMock = stubFetch(Response.json(DTO, { status: 201 }));

    await createCategory(INPUT);

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/admin/categories');
    expect(init?.method).toBe('POST');
    expect(init?.headers).toMatchObject({ 'X-Requested-With': 'XMLHttpRequest' });
    expect(bodyOf(init)).toEqual(INPUT);
  });

  it('updates with PUT and sends the version the form was loaded with', async () => {
    const fetchMock = stubFetch(Response.json(DTO));

    await updateCategory(DTO.id, INPUT, 7);

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe(`/api/v1/admin/categories/${DTO.id}`);
    expect(init?.method).toBe('PUT');
    expect(bodyOf(init)).toEqual({ ...INPUT, version: 7 });
  });

  it.each([
    [true, 'activate'],
    [false, 'deactivate'],
  ])('sets active=%s through the %s address with the version', async (isActive, action) => {
    const fetchMock = stubFetch(Response.json(DTO));

    await setCategoryActive(DTO.id, isActive, 7);

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe(`/api/v1/admin/categories/${DTO.id}/${action}`);
    expect(init?.method).toBe('POST');
    expect(bodyOf(init)).toEqual({ version: 7 });
  });

  it('moves a category and returns the whole list', async () => {
    const fetchMock = stubFetch(Response.json([DTO]));

    const list = await moveCategory(DTO.id, 'Up', 7);

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe(`/api/v1/admin/categories/${DTO.id}/move`);
    expect(bodyOf(init)).toEqual({ direction: 'Up', version: 7 });
    expect(list).toHaveLength(1);
  });

  it('throws an ApiError with the status and the code of the problem', async () => {
    stubFetch(Response.json({ code: 'category.slug_taken' }, { status: 409 }));

    const error = await createCategory(INPUT).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 409, code: 'category.slug_taken' });
  });

  it('fails loudly on an integer that is not a safe integer', async () => {
    stubFetch(Response.json([{ ...DTO, version: 'abc' }]));

    await expect(getAdminCategories()).rejects.toThrow(RangeError);
  });
});
