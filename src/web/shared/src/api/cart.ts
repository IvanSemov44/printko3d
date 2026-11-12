import type { components } from './schema';
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
import type { FulfilmentType } from './client';
import { apiDelete, apiRequest, apiSend } from './http';

type CartLineStateDto = components['schemas']['CartLineState'];

/** What the shop can say about one line (decision 0027). Rule 21 widens what each state means for the buyer. */
export type CartLineState = CartLineStateDto;

const LINE_STATES: readonly CartLineState[] = [
  'Available',
  'InsufficientStock',
  'Inactive',
  'Archived',
];

const FULFILMENT_TYPES: readonly FulfilmentType[] = ['InStock', 'MadeToOrder'];

/** The image of a line; the address is built from the key with `imageUrl`, as for the catalog. */
export interface CartLineImage {
  storageKey: string;
  altText: string;
}

/** A line of the cart. Price, name and SKU are read from the variant by the server on every answer. */
export interface CartLine {
  variantId: string;
  productName: string;
  productSlug: string;
  variantName: string;
  sku: string;
  image: CartLineImage | null;
  /** In the smallest unit of the currency (rule 10). */
  unitPrice: number;
  quantity: number;
  linePrice: number;
  state: CartLineState;
  fulfilmentType: FulfilmentType;
  /** The days a made-to-order line takes to make; null when the shop gave none. */
  leadTimeDays: number | null;
  /** The most pieces the buyer can take (the stock of an in-stock line); null for a made-to-order line. */
  availableQuantity: number | null;
}

/** The cart as the server counts it. The shop shows these numbers and never adds them up itself. */
export interface Cart {
  lines: CartLine[];
  itemCount: number;
  total: number;
}

const readImage: Reader<CartLineImage> = (value, path) => {
  const object = readObject(value, path);
  return {
    storageKey: field(object, 'storageKey', readString, path),
    altText: field(object, 'altText', readString, path),
  };
};

const readLine: Reader<CartLine> = (value, path) => {
  const object = readObject(value, path);
  return {
    variantId: field(object, 'variantId', readString, path),
    productName: field(object, 'productName', readString, path),
    productSlug: field(object, 'productSlug', readString, path),
    variantName: field(object, 'variantName', readString, path),
    sku: field(object, 'sku', readString, path),
    image: field(object, 'image', readNullable(readImage), path),
    unitPrice: field(object, 'unitPrice', readInteger, path),
    quantity: field(object, 'quantity', readInteger, path),
    linePrice: field(object, 'linePrice', readInteger, path),
    state: field(object, 'state', readOneOf(LINE_STATES), path),
    fulfilmentType: field(object, 'fulfilmentType', readOneOf(FULFILMENT_TYPES), path),
    leadTimeDays: field(object, 'leadTimeDays', readNullable(readInteger), path),
    availableQuantity: field(object, 'availableQuantity', readNullable(readInteger), path),
  };
};

function readCart(value: unknown): Cart {
  const object = readObject(value, 'cart');
  return {
    lines: field(object, 'lines', readArray(readLine), 'cart'),
    itemCount: field(object, 'itemCount', readInteger, 'cart'),
    total: field(object, 'total', readInteger, 'cart'),
  };
}

const CART_URL = '/api/v1/cart';

function itemUrl(variantId: string): string {
  return `${CART_URL}/items/${encodeURIComponent(variantId)}`;
}

/** Reading never creates a cart or a cookie: a new visitor gets an empty cart. */
export async function getCart(signal?: AbortSignal): Promise<Cart> {
  return readCart(await apiRequest(CART_URL, { signal }));
}

export async function addCartItem(
  variantId: string,
  quantity: number,
  signal?: AbortSignal,
): Promise<Cart> {
  return readCart(await apiSend('POST', `${CART_URL}/items`, { variantId, quantity }, signal));
}

export async function setCartItemQuantity(
  variantId: string,
  quantity: number,
  signal?: AbortSignal,
): Promise<Cart> {
  return readCart(await apiSend('PUT', itemUrl(variantId), { quantity }, signal));
}

export async function removeCartItem(variantId: string, signal?: AbortSignal): Promise<Cart> {
  return readCart(await apiDelete(itemUrl(variantId), signal));
}

export async function clearCart(signal?: AbortSignal): Promise<Cart> {
  return readCart(await apiDelete(CART_URL, signal));
}
