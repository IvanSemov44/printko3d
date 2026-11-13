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
  /** The total of the cart the buyer saw, in the smallest unit of the currency. The server refuses a different one. */
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
  /** Only `New` exists for now; more come with payment and the statuses (tasks 26 and 27). */
  status: string;
  currency: string;
  total: number;
  customerName: string;
  email: string;
  phone: string;
  shippingStreet: string;
  shippingCity: string;
  shippingPostalCode: string;
  shippingCountry: string;
  createdAt: string;
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
    customerName: field(object, 'customerName', readString, 'order'),
    email: field(object, 'email', readString, 'order'),
    phone: field(object, 'phone', readString, 'order'),
    shippingStreet: field(object, 'shippingStreet', readString, 'order'),
    shippingCity: field(object, 'shippingCity', readString, 'order'),
    shippingPostalCode: field(object, 'shippingPostalCode', readString, 'order'),
    shippingCountry: field(object, 'shippingCountry', readString, 'order'),
    createdAt: field(object, 'createdAt', readString, 'order'),
    items: field(object, 'items', readArray(readItem), 'order'),
  };
}

/**
 * Turns the cart of the cookie into an order. The server reads the prices again and empties the cart.
 * A refusal is an ApiError: 400 `validation.failed` (with the fields), 409 `order.total_changed` or `cart.changed`,
 * 422 `order.cart_empty` or `order.cart_not_orderable`.
 */
export async function placeOrder(input: PlaceOrderInput, signal?: AbortSignal): Promise<Order> {
  return readOrder(await apiSend('POST', '/api/v1/orders', input, signal));
}
