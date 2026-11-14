import type { MessageKey } from '../../i18n';

/** The text for a delivery method by its code. A method the shop has no text for yet is just "Delivery". */
export function shippingMethodNameKey(code: string): MessageKey {
  return code === 'address' ? 'shipping.method.address' : 'shipping.method.unknown';
}
