import { field, readBoolean, readObject, readOneOf, readString } from './decode';
import { apiAct, apiRequest, apiSend } from './http';

/** An open payment of an order (decision 0035). The buyer pays at `redirectUrl`; the card is never typed in the shop. */
export interface PaymentSession {
  /** Random identifier that the shop uses to ask about the payment. It does not reveal the order. */
  paymentId: string;
  /** An address inside the shop for the test provider, or the page of the provider. */
  redirectUrl: string;
  /** The last moment to pay. */
  expiresAt: string;
}

/**
 * Where a payment stands. `Expired` is a payment that got no answer before the order was cancelled; `PaidWithoutOrder`
 * is money that came after the order was cancelled and the stock was gone, which the shop returns by hand.
 */
export type PaymentState = 'Pending' | 'Succeeded' | 'Failed' | 'PaidWithoutOrder' | 'Expired';

const PAYMENT_STATES: readonly PaymentState[] = [
  'Pending',
  'Succeeded',
  'Failed',
  'PaidWithoutOrder',
  'Expired',
];

/** How a test payment ends (the test page only). */
export type TestPaymentOutcome = 'Paid' | 'Failed';

/**
 * Makes sure the order has a payment waiting at the provider and returns it; a second call returns the same one.
 * A refusal is an ApiError: 404 `order.not_found` or `payment.disabled`, 409 `payment.order_not_payable` (paid,
 * cancelled or past its deadline).
 */
export async function startPayment(
  orderNumber: number,
  signal?: AbortSignal,
): Promise<PaymentSession> {
  const object = readObject(
    await apiAct('PUT', `/api/v1/orders/${String(orderNumber)}/payment`, signal),
    'payment',
  );
  return {
    paymentId: field(object, 'paymentId', readString, 'payment'),
    redirectUrl: field(object, 'redirectUrl', readString, 'payment'),
    expiresAt: field(object, 'expiresAt', readString, 'payment'),
  };
}

/** The state of a payment and nothing else. A refusal is an ApiError: 404 `payment.not_found`. */
export async function getPaymentState(
  paymentId: string,
  signal?: AbortSignal,
): Promise<PaymentState> {
  const object = readObject(
    await apiRequest(`/api/v1/payments/${encodeURIComponent(paymentId)}`, { signal }),
    'payment',
  );
  return field(object, 'status', readOneOf(PAYMENT_STATES), 'payment');
}

/** Whether the payment is a test one, so the shop can say so. False when a real provider takes the money. */
export async function getPaymentMode(signal?: AbortSignal): Promise<{ isTest: boolean }> {
  const object = readObject(await apiRequest('/api/v1/payments/mode', { signal }), 'paymentMode');
  return { isTest: field(object, 'isTest', readBoolean, 'paymentMode') };
}

/**
 * The test page ends a payment. The sum is the one of the payment, never one the page sends. A refusal is an ApiError:
 * 404 `payment.not_found`, 422 `payment.session_expired` (a payment after the session ended).
 */
export async function completeTestPayment(
  reference: string,
  outcome: TestPaymentOutcome,
  signal?: AbortSignal,
): Promise<void> {
  await apiSend(
    'POST',
    `/api/v1/payments/test/${encodeURIComponent(reference)}/complete`,
    { outcome },
    signal,
  );
}
