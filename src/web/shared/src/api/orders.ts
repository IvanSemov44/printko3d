import type { FulfilmentType } from './client';
import {
  field,
  readArray,
  readInteger,
  readNullable,
  readObject,
  readOneOf,
  readString,
  type Reader,
} from './decode';
import { apiSend } from './http';

const FULFILMENT_TYPES: readonly FulfilmentType[] = ['InStock', 'MadeToOrder'];

/** What the buyer fills in. The country is always Bulgaria for now, so it is not sent (decision 0032). */
export interface PlaceOrderInput {
  name: string;
  email: string;
  phone: string;
  street: string;
  city: string;
  postalCode: string;
  termsAccepted: boolean;
  /** The code of the delivery method the buyer chose, from `getShippingOptions` (decision 0034). */
  shippingMethod: string;
  /** The total the buyer saw, with the delivery, in the smallest unit of the currency. The server refuses a different one. */
  expectedTotal: number;
}

/** A line of an order: a copy of the name, SKU and price from the moment of purchase. */
export interface OrderItem {
  productName: string;
  variantName: string;
  sku: string;
  /** In the smallest unit of the currency (rule 10). */
  unitPrice: number;
  quantity: number;
  lineTotal: number;
  fulfilmentType: FulfilmentType;
  leadTimeDays: number | null;
}

/** An order as it was placed. Its number is random and is shown as `PR-` and the digits. */
export interface Order {
  number: number;
  /** One of `ORDER_STATUSES` (decision 0036). */
  status: string;
  currency: string;
  /** The lines plus `shippingAmount`. */
  total: number;
  /** The delivery the buyer chose, copied at the moment of purchase. Empty for an order made before delivery was priced. */
  shippingMethod: string;
  shippingAmount: number;
  customerName: string;
  email: string;
  phone: string;
  shippingStreet: string;
  shippingCity: string;
  shippingPostalCode: string;
  shippingCountry: string;
  createdAt: string;
  /** The last moment to pay, or null for an order made before payment existed (decision 0035). */
  paymentDueAt: string | null;
  items: OrderItem[];
}

const readItem: Reader<OrderItem> = (value, path) => {
  const object = readObject(value, path);
  return {
    productName: field(object, 'productName', readString, path),
    variantName: field(object, 'variantName', readString, path),
    sku: field(object, 'sku', readString, path),
    unitPrice: field(object, 'unitPrice', readInteger, path),
    quantity: field(object, 'quantity', readInteger, path),
    lineTotal: field(object, 'lineTotal', readInteger, path),
    fulfilmentType: field(object, 'fulfilmentType', readOneOf(FULFILMENT_TYPES), path),
    leadTimeDays: field(object, 'leadTimeDays', readNullable(readInteger), path),
  };
};

/** Reads an order from an answer or from the state of a route; a wrong shape throws an UnexpectedResponseError. */
export function readOrder(value: unknown): Order {
  const object = readObject(value, 'order');
  return {
    number: field(object, 'number', readInteger, 'order'),
    status: field(object, 'status', readString, 'order'),
    currency: field(object, 'currency', readString, 'order'),
    total: field(object, 'total', readInteger, 'order'),
    shippingMethod: field(object, 'shippingMethod', readString, 'order'),
    shippingAmount: field(object, 'shippingAmount', readInteger, 'order'),
    customerName: field(object, 'customerName', readString, 'order'),
    email: field(object, 'email', readString, 'order'),
    phone: field(object, 'phone', readString, 'order'),
    shippingStreet: field(object, 'shippingStreet', readString, 'order'),
    shippingCity: field(object, 'shippingCity', readString, 'order'),
    shippingPostalCode: field(object, 'shippingPostalCode', readString, 'order'),
    shippingCountry: field(object, 'shippingCountry', readString, 'order'),
    createdAt: field(object, 'createdAt', readString, 'order'),
    paymentDueAt: field(object, 'paymentDueAt', readNullable(readString), 'order'),
    items: field(object, 'items', readArray(readItem), 'order'),
  };
}

/**
 * Turns the cart of the cookie into an order. The server reads the prices again and empties the cart.
 * A refusal is an ApiError: 400 `validation.failed` (with the fields), 409 `order.total_changed`, `order.stock_changed` or `cart.changed`,
 * 422 `order.cart_empty`, `order.cart_not_orderable`, `order.shipping_unavailable` or `shipping.too_heavy`.
 */
export async function placeOrder(input: PlaceOrderInput, signal?: AbortSignal): Promise<Order> {
  return readOrder(await apiSend('POST', '/api/v1/orders', input, signal));
}

/** The statuses of an order in the order they follow each other, and `Cancelled` at the end (decision 0036). */
export const ORDER_STATUSES = [
  'New',
  'Paid',
  'InProduction',
  'ReadyToShip',
  'Shipped',
  'Completed',
  'Cancelled',
] as const;

/**
 * Finds an order again by its number and the email of the order (page "My order", decision 0036). A refusal is an
 * ApiError: 400 `validation.failed` (a field is empty), 404 `order.not_found` (the same for a wrong number and a wrong
 * email, and for a number closed after too many wrong emails), 429 (too many requests).
 */
export async function findOrder(
  number: number,
  email: string,
  signal?: AbortSignal,
): Promise<Order> {
  return readOrder(await apiSend('POST', '/api/v1/orders/lookup', { number, email }, signal));
}

/** Finds the order that the key in the link of an email belongs to. A refusal is an ApiError: 404 `order.not_found`, 429. */
export async function findOrderByToken(token: string, signal?: AbortSignal): Promise<Order> {
  return readOrder(await apiSend('POST', '/api/v1/orders/lookup-by-token', { token }, signal));
}
