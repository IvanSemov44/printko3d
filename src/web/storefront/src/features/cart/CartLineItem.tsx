import { formatMoney, imageUrl, type CartLine } from '@storefront/shared';
import { Link } from 'react-router';
import { ImagePlaceholder } from '../product/ImagePlaceholder';
import { useRuntimeConfigQuery } from '../store-settings/useRuntimeConfigQuery';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';
import { describeAvailability } from '../catalog/availability';
import { cartErrorKey } from './cartErrors';
import { canIncrease, lineStateNoteKey } from './lineState';
import { QuantityStepper } from './QuantityStepper';
import { useIsLineBusy, useRemoveFromCart, useSetCartQuantity } from './useCartMutations';

interface CartLineItemProps {
  line: CartLine;
  /** Called when the buyer follows the link to the product, so a panel can close. */
  onNavigate?: () => void;
}

/** One line: picture, names, stepper, price and "Remove". Every number comes from the server. */
export function CartLineItem({ line, onNavigate }: CartLineItemProps) {
  const t = useT();
  const { defaultCurrency, defaultLocale } = useStoreSettings();
  const { data: config } = useRuntimeConfigQuery();
  const setQuantity = useSetCartQuantity(line.variantId);
  const remove = useRemoveFromCart(line.variantId);
  const busy = useIsLineBusy(line.variantId);

  const money = (amount: number) => formatMoney(amount, defaultCurrency, defaultLocale);
  const name = `${line.productName}, ${line.variantName}`;
  const address = line.image ? imageUrl(config?.imageBaseUrl ?? null, line.image.storageKey) : null;
  const noteKey = lineStateNoteKey(line.state);
  // A product that is no longer sold stays in the cart, dimmed, so the buyer sees why and can take it out.
  const isGone = line.state === 'Inactive' || line.state === 'Archived';
  const error: unknown = setQuantity.error ?? remove.error;

  return (
    <li className={isGone ? 'cart-line cart-line-gone' : 'cart-line'}>
      <div className="cart-line-media">
        {address !== null ? (
          <img src={address} alt="" width={64} height={64} loading="lazy" />
        ) : (
          <ImagePlaceholder />
        )}
      </div>
      <div className="cart-line-body">
        <p className="cart-line-name">
          <Link
            to={`/products/${encodeURIComponent(line.productSlug)}?variant=${encodeURIComponent(line.sku)}`}
            onClick={onNavigate}
          >
            {line.productName}
          </Link>
        </p>
        <p className="cart-line-variant">{line.variantName}</p>
        {line.fulfilmentType === 'MadeToOrder' && (
          <p className="cart-line-availability">
            {describeAvailability(t, defaultLocale, {
              fulfilmentType: line.fulfilmentType,
              isAvailable: true,
              leadTimeDays: line.leadTimeDays,
            })}
          </p>
        )}
        {noteKey !== null && <p className="cart-line-note">{t(noteKey)}</p>}
        <p className="cart-line-price">
          <span className="visually-hidden">{t('cart.unitPrice')} </span>
          {money(line.unitPrice)}
        </p>
        <div className="cart-line-actions">
          <QuantityStepper
            name={name}
            quantity={line.quantity}
            canIncrease={canIncrease(line)}
            busy={busy}
            onChange={(quantity) => {
              setQuantity.mutate(quantity);
            }}
          />
          <button
            type="button"
            className="button-text"
            aria-label={t('cart.removeNamed', { name })}
            aria-disabled={busy}
            onClick={() => {
              if (!busy) {
                remove.mutate();
              }
            }}
          >
            {t('cart.remove')}
          </button>
        </div>
        {error !== null && error !== undefined && (
          <p className="cart-line-error" role="alert">
            {t(cartErrorKey(error))}
          </p>
        )}
      </div>
      <p className="cart-line-total">
        <span className="visually-hidden">{t('cart.linePrice')} </span>
        {money(line.linePrice)}
      </p>
    </li>
  );
}
