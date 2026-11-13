import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from './client';
import { placeOrder, type PlaceOrderInput } from './orders';
import { UnexpectedResponseError } from './decode';

const INPUT: PlaceOrderInput = {
  name: 'Test Buyer',
  email: 'buyer@example.test',
  phone: '+359 88 000 0000',
  street: '1 Test Street',
  city: 'Sofia',
  postalCode: '1000',
  termsAccepted: true,
  expectedTotal: 3000,
};

const DTO = {
  number: 12345678,
  status: 'New',
  currency: 'BGN',
  total: '3000',
  customerName: 'Test Buyer',
  email: 'buyer@example.test',
  phone: '+359 88 000 0000',
  shippingStreet: '1 Test Street',
  shippingCity: 'Sofia',
  shippingPostalCode: '1000',
  shippingCountry: 'BG',
  createdAt: '2026-11-13T10:00:00+00:00',
  items: [
    {
      productName: 'Dragon',
      variantName: 'Blue',
      sku: 'DRG-001',
      unitPrice: '1500',
      quantity: 2,
      lineTotal: 3000,
      fulfilmentType: 'MadeToOrder',
      leadTimeDays: 7,
    },
  ],
};

function stubFetch(body: unknown, status = 201) {
  const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(() =>
    Promise.resolve(Response.json(body, { status })),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('placeOrder', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends the fields with the protective header, and reads the order that comes back', async () => {
    const fetchMock = stubFetch(DTO);

    const order = await placeOrder(INPUT);

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/orders');
    expect(init?.method).toBe('POST');
    expect(new Headers(init?.headers).get('X-Requested-With')).toBe('XMLHttpRequest');
    expect(typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined).toEqual(
      INPUT,
    );
    expect(order).toMatchObject({
      number: 12345678,
      status: 'New',
      currency: 'BGN',
      total: 3000,
      shippingCountry: 'BG',
    });
    expect(order.items).toEqual([
      {
        productName: 'Dragon',
        variantName: 'Blue',
        sku: 'DRG-001',
        unitPrice: 1500,
        quantity: 2,
        lineTotal: 3000,
        fulfilmentType: 'MadeToOrder',
        leadTimeDays: 7,
      },
    ]);
  });

  it('names the field when the answer has the wrong shape', async () => {
    stubFetch({ ...DTO, number: null });

    await expect(placeOrder(INPUT)).rejects.toThrow(UnexpectedResponseError);
    await expect(placeOrder(INPUT)).rejects.toThrow(/order\.number/);
  });

  it('refuses a line with an unknown kind of fulfilment', async () => {
    stubFetch({ ...DTO, items: [{ ...DTO.items[0], fulfilmentType: 'Dropship' }] });

    await expect(placeOrder(INPUT)).rejects.toThrow(/fulfilmentType/);
  });

  it('turns a refusal into an ApiError with the code and the fields', async () => {
    stubFetch(
      { code: 'validation.failed', errors: { email: ['bad'], termsAccepted: ['no'] } },
      400,
    );

    const error: unknown = await placeOrder(INPUT).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      status: 400,
      code: 'validation.failed',
      fields: ['email', 'termsAccepted'],
    });
  });
});
