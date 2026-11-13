import { formatMoney, type ProductDetails, type ProductVariant } from '@storefront/shared';
import { availabilityKind, describeAvailability } from '../../components/availability';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';

interface ProductOfferProps {
  product: ProductDetails;
  variant: ProductVariant;
}

/** Price and availability of the selected variant. A polite live region, so a change is announced. */
export function ProductOffer({ product, variant }: ProductOfferProps) {
  const t = useT();
  const { defaultCurrency, defaultLocale } = useStoreSettings();
  const money = (amount: number) => formatMoney(amount, defaultCurrency, defaultLocale);

  const availability = describeAvailability(t, defaultLocale, {
    fulfilmentType: product.fulfilmentType,
    leadTimeDays: product.leadTimeDays,
    isAvailable: variant.isAvailable,
  });
  const oldPrice =
    variant.compareAtPrice !== null && variant.compareAtPrice > variant.price
      ? variant.compareAtPrice
      : null;

  return (
    <div className="product-offer" aria-live="polite" aria-atomic="true">
      <p className="product-price">
        {oldPrice !== null && (
          <>
            <s className="old-price">
              <span className="visually-hidden">{t('product.oldPrice')} </span>
              {money(oldPrice)}
            </s>{' '}
          </>
        )}
        <span className="visually-hidden">{t('product.price')} </span>
        {money(variant.price)}
      </p>
      <p>
        <span
          className={`badge badge-${availabilityKind({ fulfilmentType: product.fulfilmentType, isAvailable: variant.isAvailable })}`}
        >
          {availability}
        </span>
      </p>
    </div>
  );
}
