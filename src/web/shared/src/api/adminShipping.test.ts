import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from './client';
import { UnexpectedResponseError } from './decode';
import { getShippingTariff, saveShippingTariff } from './adminShipping';

function stubFetch(body: unknown, status = 200) {
  const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(() =>
    Promise.resolve(Response.json(body, { status })),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('the delivery tariff of the admin API', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('reads the steps, with a 64-bit price sent as text', async () => {
    const fetchMock = stubFetch({ steps: [{ maxWeightGrams: 1000, price: '600' }] });

    const steps = await getShippingTariff();

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/admin/shipping-tariff');
    expect(steps).toEqual([{ maxWeightGrams: 1000, price: 600 }]);
  });

  it('sends the whole tariff with the protective header and reads the sorted answer', async () => {
    const fetchMock = stubFetch({ steps: [{ maxWeightGrams: 500, price: 400 }] });

    const steps = await saveShippingTariff([{ maxWeightGrams: 500, price: 400 }]);

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/admin/shipping-tariff');
    expect(init?.method).toBe('PUT');
    expect(new Headers(init?.headers).get('X-Requested-With')).toBe('XMLHttpRequest');
    expect(typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined).toEqual(
      { steps: [{ maxWeightGrams: 500, price: 400 }] },
    );
    expect(steps).toEqual([{ maxWeightGrams: 500, price: 400 }]);
  });

  it('names the field that is wrong or missing', async () => {
    stubFetch({ steps: [{ maxWeightGrams: 1000 }] });

    await expect(getShippingTariff()).rejects.toThrow(UnexpectedResponseError);
    await expect(getShippingTariff()).rejects.toThrow(/price/);
  });

  it('lets a refusal of the server through with its code', async () => {
    stubFetch({ code: 'shipping.tariff_conflict' }, 409);

    const error: unknown = await saveShippingTariff([]).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).code).toBe('shipping.tariff_conflict');
  });
});
