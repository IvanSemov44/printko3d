import { Link, useLocation } from 'react-router';
import { useStoreSettings } from '../store-settings/useStoreSettings';
import { badgeText, cartButtonLabel } from './itemCount';
import { useCartPanel } from './useCartPanel';
import { useCartQuery } from '../../components/useCartQuery';

export const CART_PATH = '/cart';

/**
 * The cart in the header: the bag, with the number of pieces from the server. Opens the panel. On the cart page itself
 * there is nothing to open, so it is a link to the page.
 */
export function CartButton() {
  const { defaultLocale } = useStoreSettings();
  const { open } = useCartPanel();
  const { pathname } = useLocation();
  const { data } = useCartQuery();
  const count = data?.itemCount ?? 0;

  const label = cartButtonLabel(defaultLocale, count);
  const content = (
    <>
      <svg viewBox="0 0 24 24" width="24" height="24" aria-hidden="true" focusable="false">
        <path
          d="M6 7h12l1 13H5L6 7Zm3 0a3 3 0 0 1 6 0"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinejoin="round"
          strokeLinecap="round"
        />
      </svg>
      {count > 0 && (
        <span className="cart-badge" aria-hidden="true">
          {badgeText(count)}
        </span>
      )}
    </>
  );

  if (pathname === CART_PATH) {
    return (
      <Link
        to={CART_PATH}
        className="button button-text cart-button"
        aria-label={label}
        aria-current="page"
      >
        {content}
      </Link>
    );
  }
  return (
    <button type="button" className="cart-button button-text" aria-label={label} onClick={open}>
      {content}
    </button>
  );
}
