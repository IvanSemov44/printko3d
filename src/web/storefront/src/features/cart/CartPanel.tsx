import { formatMoney } from '@storefront/shared';
import { useEffect, useId, useRef } from 'react';
import { Link } from 'react-router';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';
import { CartLines } from './CartLines';
import { useCartPanel } from './useCartPanel';
import { useCartQuery } from './useCartQuery';

/**
 * The cart from the right, made with the browser's own dialog: the page behind it cannot be reached, Esc closes it
 * and the focus stays inside. Clicking the dark area outside closes it too.
 */
export function CartPanel() {
  const t = useT();
  const { defaultCurrency, defaultLocale } = useStoreSettings();
  const { isOpen, close, returnFocus } = useCartPanel();
  const { data: cart, isError, refetch } = useCartQuery();
  const dialog = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const element = dialog.current;
    if (!element) {
      return;
    }
    if (isOpen && !element.open) {
      element.showModal();
    } else if (!isOpen && element.open) {
      element.close();
      returnFocus();
    }
  }, [isOpen, returnFocus]);

  return (
    <dialog
      ref={dialog}
      className="cart-panel"
      aria-labelledby={titleId}
      onCancel={(event) => {
        // Esc: the state decides, so the dialog and the page never disagree.
        event.preventDefault();
        close();
      }}
      onClick={(event) => {
        // The dialog has no padding, so a click on the element itself is a click on the dark area.
        if (event.target === event.currentTarget) {
          close();
        }
      }}
    >
      <div className="cart-panel-header">
        <h2 id={titleId}>{t('cart.title')}</h2>
        <button
          type="button"
          className="button-secondary cart-panel-close"
          aria-label={t('cart.close')}
          onClick={close}
        >
          <span aria-hidden="true">×</span>
        </button>
      </div>
      <div className="cart-panel-body">
        {isError && (
          <div role="alert">
            <p>{t('cart.loadError')}</p>
            <button
              type="button"
              className="button-secondary"
              onClick={() => {
                void refetch();
              }}
            >
              {t('cart.retry')}
            </button>
          </div>
        )}
        {!isError && !cart && <p role="status">{t('cart.loading')}</p>}
        {cart && cart.lines.length === 0 && (
          <div className="cart-empty">
            <p>{t('cart.empty')}</p>
            <Link className="button button-secondary" to="/" onClick={close}>
              {t('cart.emptyAction')}
            </Link>
          </div>
        )}
        {cart && cart.lines.length > 0 && <CartLines cart={cart} onNavigate={close} />}
      </div>
      {cart && cart.lines.length > 0 && (
        <div className="cart-panel-footer">
          <p className="cart-subtotal">
            <span>{t('cart.subtotal')}</span>
            <strong>{formatMoney(cart.total, defaultCurrency, defaultLocale)}</strong>
          </p>
          <p className="cart-delivery-note">{t('cart.deliveryNote')}</p>
          <Link className="button" to="/cart" onClick={close}>
            {t('cart.viewCart')}
          </Link>
        </div>
      )}
    </dialog>
  );
}
