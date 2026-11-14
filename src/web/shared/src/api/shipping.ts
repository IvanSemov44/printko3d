import { field, readArray, readInteger, readObject, readString, type Reader } from './decode';
import { apiRequest } from './http';

/** A way to deliver the cart and what it costs (decision 0034). The shop shows the name of a method by its `code`. */
export interface ShippingQuote {
  code: string;
  /** In the smallest unit of the currency (rule 10). */
  amount: number;
}

/** The delivery methods for the cart. The total of the order is `itemsTotal` plus the amount of the chosen method. */
export interface ShippingOptions {
  /** The total of the lines that can be ordered, without the delivery. */
  itemsTotal: number;
  methods: ShippingQuote[];
}

const readQuote: Reader<ShippingQuote> = (value, path) => {
  const object = readObject(value, path);
  return {
    code: field(object, 'code', readString, path),
    amount: field(object, 'amount', readInteger, path),
  };
};

/**
 * What delivery costs for the cart of the cookie. Never creates a cart. A refusal is an ApiError: 422 `order.cart_empty`
 * (nothing to deliver) or 422 `shipping.too_heavy` (the cart is heavier than any delivery carries).
 */
export async function getShippingOptions(signal?: AbortSignal): Promise<ShippingOptions> {
  const value = await apiRequest('/api/v1/shipping/quotes', { signal });
  const object = readObject(value, 'shipping');
  return {
    itemsTotal: field(object, 'itemsTotal', readInteger, 'shipping'),
    methods: field(object, 'methods', readArray(readQuote), 'shipping'),
  };
}
