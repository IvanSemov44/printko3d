import { formatMoney, imageUrl, type Cart } from '@storefront/shared';
import { describeAvailability } from '../../components/availability';
import { ImagePlaceholder } from '../../components/ImagePlaceholder';
import { lineStateNoteKey } from '../../components/lineStateNote';
import { useRuntimeConfigQuery } from '../store-settings/useRuntimeConfigQuery';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';

/** The lines of the order to check before confirming: the same look as the cart, with nothing to change. */
export function CheckoutLines({ cart }: { cart: Cart }) {
  const t = useT();
  const { defaultCurrency, defaultLocale } = useStoreSettings();
  const { data: config } = useRuntimeConfigQuery();
  const money = (amount: number) => formatMoney(amount, defaultCurrency, defaultLocale);

  return (
    <ul className="cart-lines" aria-label={t('checkout.lines')}>
      {cart.lines.map((line) => {
        const address = line.image
          ? imageUrl(config?.imageBaseUrl ?? null, line.image.storageKey)
          : null;
        const noteKey = lineStateNoteKey(line.state);
        return (
          <li
            key={line.variantId}
            className={line.state === 'Available' ? 'cart-line' : 'cart-line cart-line-gone'}
          >
            <div className="cart-line-media">
              {address !== null ? (
                <img src={address} alt="" width={64} height={64} loading="lazy" />
              ) : (
                <ImagePlaceholder />
              )}
            </div>
            <div className="cart-line-body">
              <p className="cart-line-name">{line.productName}</p>
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
                {t('checkout.lineQuantity', {
                  quantity: String(line.quantity),
                  price: money(line.unitPrice),
                })}
              </p>
            </div>
            <p className="cart-line-total">
              <span className="visually-hidden">{t('cart.linePrice')} </span>
              {money(line.linePrice)}
            </p>
          </li>
        );
      })}
    </ul>
  );
}
