import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from './client';
import { UnexpectedResponseError } from './decode';
import { getShippingOptions } from './shipping';

function stubFetch(body: unknown, status = 200) {
  const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(() =>
    Promise.resolve(Response.json(body, { status })),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('getShippingOptions', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('reads the methods and the total of the lines, with a 64-bit number sent as text', async () => {
    const fetchMock = stubFetch({
      itemsTotal: '4500',
      methods: [{ code: 'address', amount: 600 }],
    });

    const options = await getShippingOptions();

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/shipping/quotes');
    expect(options).toEqual({ itemsTotal: 4500, methods: [{ code: 'address', amount: 600 }] });
  });

  it('reads a list with no method', async () => {
    stubFetch({ itemsTotal: 4500, methods: [] });

    expect((await getShippingOptions()).methods).toEqual([]);
  });

  it('names the field that is wrong or missing', async () => {
    stubFetch({ itemsTotal: 4500, methods: [{ code: 'address' }] });

    await expect(getShippingOptions()).rejects.toThrow(UnexpectedResponseError);
    await expect(getShippingOptions()).rejects.toThrow(/amount/);
  });

  it('lets a refusal of the server through with its code', async () => {
    stubFetch({ status: 422, code: 'shipping.too_heavy' }, 422);

    const error: unknown = await getShippingOptions().catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).code).toBe('shipping.too_heavy');
  });
});
