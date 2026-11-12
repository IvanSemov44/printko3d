import type { Cart } from '@storefront/shared';
import { useT } from '../store-settings/useStoreSettings';
import { CartLineItem } from './CartLineItem';

/** The lines of the cart, in the order the server gives them. The server allows at most 50 lines, so there is no paging. */
export function CartLines({ cart, onNavigate }: { cart: Cart; onNavigate?: () => void }) {
  const t = useT();
  return (
    <ul className="cart-lines" aria-label={t('cart.lines')}>
      {cart.lines.map((line) => (
        <CartLineItem key={line.variantId} line={line} onNavigate={onNavigate} />
      ))}
    </ul>
  );
}
