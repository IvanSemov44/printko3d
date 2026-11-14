import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from './client';
import {
  findOrder,
  findOrderByToken,
  ORDER_STATUSES,
  placeOrder,
  readOrder,
  type PlaceOrderInput,
} from './orders';
import { UnexpectedResponseError } from './decode';

const INPUT: PlaceOrderInput = {
  name: 'Test Buyer',
  email: 'buyer@example.test',
  phone: '+359 88 000 0000',
  street: '1 Test Street',
  city: 'Sofia',
  postalCode: '1000',
  termsAccepted: true,
  shippingMethod: 'address',
  expectedTotal: 3600,
};

const DTO = {
  number: 12345678,
  status: 'New',
  currency: 'BGN',
  total: '3600',
  shippingMethod: 'address',
  shippingAmount: '600',
  customerName: 'Test Buyer',
  email: 'buyer@example.test',
  phone: '+359 88 000 0000',
  shippingStreet: '1 Test Street',
  shippingCity: 'Sofia',
  shippingPostalCode: '1000',
  shippingCountry: 'BG',
  createdAt: '2026-11-13T10:00:00+00:00',
  paymentDueAt: '2026-11-13T11:00:00+00:00',
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
      total: 3600,
      shippingMethod: 'address',
      shippingAmount: 600,
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

describe('readOrder', () => {
  it('reads the deadline of the payment, and null for an order that has none', () => {
    expect(readOrder(DTO).paymentDueAt).toBe('2026-11-13T11:00:00+00:00');
    expect(readOrder({ ...DTO, paymentDueAt: null }).paymentDueAt).toBeNull();
  });

  it('refuses a deadline that is not a string', () => {
    expect(() => readOrder({ ...DTO, paymentDueAt: 5 })).toThrow(/order\.paymentDueAt/);
  });
});

describe('findOrder', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends the number and the email in the body, with the protective header, and reads the order', async () => {
    const fetchMock = stubFetch(DTO, 200);

    const order = await findOrder(12345678, 'buyer@example.test');

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/orders/lookup');
    expect(init?.method).toBe('POST');
    expect(new Headers(init?.headers).get('X-Requested-With')).toBe('XMLHttpRequest');
    expect(typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined).toEqual(
      { number: 12345678, email: 'buyer@example.test' },
    );
    expect(order.number).toBe(12345678);
  });

  it('keeps the email out of the address', async () => {
    const fetchMock = stubFetch(DTO, 200);

    await findOrder(12345678, 'buyer@example.test');

    expect(fetchMock.mock.calls[0]?.[0]).not.toContain('buyer');
  });

  it('turns the refusal for an unknown order into an ApiError with the code', async () => {
    stubFetch({ code: 'order.not_found' }, 404);

    const error: unknown = await findOrder(1, 'a@b.test').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 404, code: 'order.not_found' });
  });
});

describe('findOrderByToken', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends the token in the body, not in the address, and reads the order', async () => {
    const fetchMock = stubFetch(DTO, 200);

    const order = await findOrderByToken('secret-token');

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/orders/lookup-by-token');
    expect(typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined).toEqual(
      { token: 'secret-token' },
    );
    expect(order.number).toBe(12345678);
  });
});

describe('ORDER_STATUSES', () => {
  it('lists the statuses of the lifecycle in order, with the cancelled one last', () => {
    expect(ORDER_STATUSES).toEqual([
      'New',
      'Paid',
      'InProduction',
      'ReadyToShip',
      'Shipped',
      'Completed',
      'Cancelled',
    ]);
  });
});
