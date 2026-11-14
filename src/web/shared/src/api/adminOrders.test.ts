import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from './client';
import { UnexpectedResponseError } from './decode';
import { cancelOrder, changeOrderStatus, getAdminOrder, getAdminOrders } from './adminOrders';

const ORDER = {
  number: 12345678,
  status: 'Paid',
  currency: 'BGN',
  total: '3000',
  shippingMethod: 'address',
  shippingAmount: 0,
  customerName: 'Test Buyer',
  email: 'buyer@example.test',
  phone: '+359000000',
  shippingStreet: '1 Test Street',
  shippingCity: 'Sofia',
  shippingPostalCode: '1000',
  shippingCountry: 'BG',
  createdAt: '2026-11-14T10:00:00Z',
  paymentDueAt: null,
  items: [],
};

function stubFetch(body: unknown, status = 200) {
  const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(() =>
    Promise.resolve(Response.json(body, { status })),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function bodyOf(init: RequestInit | undefined): unknown {
  return typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined;
}

describe('the orders of the admin API', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('asks for a page with every status repeated, and reads the rows (a 64-bit total may be text)', async () => {
    const fetchMock = stubFetch({
      items: [
        {
          number: 12345678,
          status: 'Paid',
          currency: 'BGN',
          total: '3000',
          customerName: 'Test Buyer',
          email: 'buyer@example.test',
          itemCount: 2,
          createdAt: '2026-11-14T10:00:00Z',
        },
      ],
      page: 2,
      pageSize: 20,
      totalCount: 21,
    });

    const page = await getAdminOrders({
      status: ['New', 'Paid'],
      search: 'anna',
      page: 2,
      pageSize: 20,
    });

    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      '/api/v1/admin/orders?status=New&status=Paid&search=anna&page=2&pageSize=20',
    );
    expect(page.totalPages).toBe(2);
    expect(page.items[0]).toMatchObject({
      number: 12345678,
      status: 'Paid',
      total: 3000,
      itemCount: 2,
    });
  });

  it('sends no filter that is empty', async () => {
    const fetchMock = stubFetch({ items: [], page: 1, pageSize: 20, totalCount: 0 });

    await getAdminOrders({ page: 1, pageSize: 20 });

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/admin/orders?page=1&pageSize=20');
  });

  it('reads one order with its next steps', async () => {
    const fetchMock = stubFetch({ order: ORDER, nextStatuses: ['InProduction'], canCancel: true });

    const result = await getAdminOrder(12345678);

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/admin/orders/12345678');
    expect(result.order.total).toBe(3000);
    expect(result.nextStatuses).toEqual(['InProduction']);
    expect(result.canCancel).toBe(true);
  });

  it('names the field of a status it does not know', async () => {
    stubFetch({ order: ORDER, nextStatuses: ['Nonsense'], canCancel: true });

    await expect(getAdminOrder(12345678)).rejects.toThrow(UnexpectedResponseError);
    await expect(getAdminOrder(12345678)).rejects.toThrow(/nextStatuses/);
  });

  it('sends the step with the protective header', async () => {
    const fetchMock = stubFetch({ order: ORDER, nextStatuses: [], canCancel: true });

    await changeOrderStatus(12345678, 'InProduction');

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/admin/orders/12345678/status');
    expect(init?.method).toBe('POST');
    expect(new Headers(init?.headers).get('X-Requested-With')).toBe('XMLHttpRequest');
    expect(bodyOf(init)).toEqual({ status: 'InProduction' });
  });

  it('refuses an order and reads whether the money goes back by hand', async () => {
    const fetchMock = stubFetch({
      order: { order: { ...ORDER, status: 'Cancelled' }, nextStatuses: [], canCancel: false },
      refundByHand: true,
    });

    const result = await cancelOrder(12345678);

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/admin/orders/12345678/cancel');
    expect(fetchMock.mock.calls[0]?.[1]?.method).toBe('POST');
    expect(result.refundByHand).toBe(true);
    expect(result.order.order.status).toBe('Cancelled');
  });

  it('lets a refusal of the server through with its code', async () => {
    stubFetch({ code: 'order.status_transition_invalid' }, 409);

    const error: unknown = await cancelOrder(12345678).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).code).toBe('order.status_transition_invalid');
  });
});
