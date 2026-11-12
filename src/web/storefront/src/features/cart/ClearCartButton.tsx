import { useEffect, useRef, useState } from 'react';
import { useT } from '../store-settings/useStoreSettings';
import { cartErrorKey } from './cartErrors';
import { useClearCart } from './useCartMutations';

/**
 * "Clear the cart" with a question first, because it cannot be undone. The question is on the page, not in a window:
 * the focus moves to the safe answer ("No") and comes back to the first button if the buyer changes their mind.
 */
export function ClearCartButton() {
  const t = useT();
  const clear = useClearCart();
  const [asking, setAsking] = useState(false);
  const askButton = useRef<HTMLButtonElement>(null);
  const keepButton = useRef<HTMLButtonElement>(null);
  const wasAsking = useRef(false);

  useEffect(() => {
    if (asking) {
      keepButton.current?.focus();
    } else if (wasAsking.current) {
      askButton.current?.focus();
    }
    wasAsking.current = asking;
  }, [asking]);

  return (
    <div className="clear-cart">
      {asking ? (
        <div role="group" aria-label={t('cart.clear')}>
          <p>{t('cart.clearConfirm')}</p>
          <div className="clear-cart-actions">
            <button
              ref={keepButton}
              type="button"
              className="button-secondary"
              onClick={() => {
                setAsking(false);
              }}
            >
              {t('cart.clearNo')}
            </button>
            <button
              type="button"
              aria-disabled={clear.isPending}
              onClick={() => {
                if (clear.isPending) {
                  return;
                }
                clear.mutate(undefined, {
                  onSuccess: () => {
                    setAsking(false);
                  },
                });
              }}
            >
              {clear.isPending ? t('cart.clearing') : t('cart.clearYes')}
            </button>
          </div>
        </div>
      ) : (
        <button
          ref={askButton}
          type="button"
          className="button-text"
          onClick={() => {
            clear.reset();
            setAsking(true);
          }}
        >
          {t('cart.clear')}
        </button>
      )}
      {clear.isError && (
        <p className="cart-line-error" role="alert">
          {t(cartErrorKey(clear.error))}
        </p>
      )}
    </div>
  );
}
