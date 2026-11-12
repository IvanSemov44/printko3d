import { useId } from 'react';
import { useT } from '../store-settings/useStoreSettings';
import { cartErrorKey } from './cartErrors';
import { useAddToCart } from './useCartMutations';
import { useCartPanel } from './useCartPanel';

interface AddToCartButtonProps {
  variantId: string;
  /** False for a variant that cannot be bought (sold out); a made-to-order variant can always be bought. */
  canBuy: boolean;
}

/** Adds one piece of the chosen variant and opens the cart. Prices and limits are the server's, not the shop's. */
export function AddToCartButton({ variantId, canBuy }: AddToCartButtonProps) {
  const t = useT();
  const { open } = useCartPanel();
  const add = useAddToCart(variantId);
  const noteId = useId();

  return (
    <div className="add-to-cart">
      <button
        type="button"
        disabled={!canBuy}
        aria-disabled={add.isPending}
        aria-describedby={canBuy ? undefined : noteId}
        onClick={() => {
          if (add.isPending) {
            return;
          }
          add.mutate(1, { onSuccess: open });
        }}
      >
        {add.isPending ? t('cart.adding') : t('cart.add')}
      </button>
      {!canBuy && (
        <p id={noteId} className="add-to-cart-note">
          {t('cart.cannotAdd')}
        </p>
      )}
      {add.isError && (
        <p className="add-to-cart-error" role="alert">
          {t(cartErrorKey(add.error))}
        </p>
      )}
    </div>
  );
}
