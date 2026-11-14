import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from './client';
import { UnexpectedResponseError } from './decode';
import { completeTestPayment, getPaymentMode, getPaymentState, startPayment } from './payments';

const SESSION = {
  paymentId: '0b1b6b1e-1f7a-4a93-9a6d-0d3c8c7d9e11',
  redirectUrl: '/pay/test/test_1_abc',
  expiresAt: '2026-11-14T11:00:00+00:00',
};

function stubFetch(body: unknown, status = 200) {
  const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(() =>
    Promise.resolve(
      status === 204 ? new Response(null, { status }) : Response.json(body, { status }),
    ),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('payments', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('opens a payment with PUT, with the protective header and no body', async () => {
    const fetchMock = stubFetch(SESSION);

    const session = await startPayment(12345678);

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/orders/12345678/payment');
    expect(init?.method).toBe('PUT');
    expect(new Headers(init?.headers).get('X-Requested-With')).toBe('XMLHttpRequest');
    expect(init?.body).toBeUndefined();
    expect(session).toEqual(SESSION);
  });

  it('names the field when the session has the wrong shape', async () => {
    stubFetch({ ...SESSION, redirectUrl: null });

    await expect(startPayment(12345678)).rejects.toThrow(UnexpectedResponseError);
    await expect(startPayment(12345678)).rejects.toThrow(/payment\.redirectUrl/);
  });

  it('turns a refusal into an ApiError with the code', async () => {
    stubFetch({ code: 'payment.order_not_payable' }, 409);

    const error: unknown = await startPayment(12345678).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 409, code: 'payment.order_not_payable' });
  });

  it.each(['Pending', 'Succeeded', 'Failed', 'PaidWithoutOrder', 'Expired'] as const)(
    'reads the state %s',
    async (status) => {
      const fetchMock = stubFetch({ status });

      expect(await getPaymentState(SESSION.paymentId)).toBe(status);
      expect(fetchMock.mock.calls[0]?.[0]).toBe(`/api/v1/payments/${SESSION.paymentId}`);
    },
  );

  it('refuses a state that it does not know', async () => {
    stubFetch({ status: 'Refunded' });

    await expect(getPaymentState(SESSION.paymentId)).rejects.toThrow(/payment\.status/);
  });

  it('reads whether the payment is a test one', async () => {
    const fetchMock = stubFetch({ isTest: true });

    expect(await getPaymentMode()).toEqual({ isTest: true });
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/payments/mode');
  });

  it('refuses a mode that is not a boolean', async () => {
    stubFetch({ isTest: 'yes' });

    await expect(getPaymentMode()).rejects.toThrow(/paymentMode\.isTest/);
  });

  it('ends a test payment with the outcome only, and the reference in the address', async () => {
    const fetchMock = stubFetch(null, 204);

    await completeTestPayment('test_1_a/b', 'Paid');

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/payments/test/test_1_a%2Fb/complete');
    expect(init?.method).toBe('POST');
    expect(new Headers(init?.headers).get('X-Requested-With')).toBe('XMLHttpRequest');
    expect(typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined).toEqual(
      {
        outcome: 'Paid',
      },
    );
  });

  it('reports the refusal of a payment after the session ended', async () => {
    stubFetch({ code: 'payment.session_expired' }, 422);

    const error: unknown = await completeTestPayment('test_1_a', 'Paid').catch(
      (caught: unknown) => caught,
    );

    expect(error).toMatchObject({ status: 422, code: 'payment.session_expired' });
  });
});
