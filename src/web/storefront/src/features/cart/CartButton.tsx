import { useStoreSettings } from '../store-settings/useStoreSettings';
import { badgeText, cartButtonLabel } from './itemCount';
import { useCartPanel } from './useCartPanel';
import { useCartQuery } from './useCartQuery';

/** The cart in the header: the bag, with the number of pieces from the server. Opens the panel. */
export function CartButton() {
  const { defaultLocale } = useStoreSettings();
  const { open } = useCartPanel();
  const { data } = useCartQuery();
  const count = data?.itemCount ?? 0;

  const label = cartButtonLabel(defaultLocale, count);

  return (
    <button type="button" className="cart-button button-text" aria-label={label} onClick={open}>
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
    </button>
  );
}
