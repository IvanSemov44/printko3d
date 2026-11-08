import { formatMoney, type ProductListItem } from '@storefront/shared';
import { Link } from 'react-router';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';
import { formatLeadTime } from './leadTime';

export function ProductCard({ product }: { product: ProductListItem }) {
  const t = useT();
  const { defaultCurrency, defaultLocale } = useStoreSettings();

  let availability: string;
  if (product.fulfilmentType === 'MadeToOrder') {
    availability = t('catalog.madeToOrder');
  } else {
    availability = product.isAvailable ? t('catalog.inStock') : t('catalog.soldOut');
  }
  const leadTime =
    product.fulfilmentType === 'MadeToOrder' && product.leadTimeDays !== null
      ? formatLeadTime(defaultLocale, product.leadTimeDays)
      : null;

  return (
    <li className="product-card">
      <h2>
        <Link to={`/products/${product.slug}`}>{product.name}</Link>
      </h2>
      <p>{product.shortDescription}</p>
      <p className="product-price">
        {t('catalog.priceFrom')} {formatMoney(product.minPrice, defaultCurrency, defaultLocale)}
      </p>
      <p>
        {availability}
        {leadTime !== null && ` · ${leadTime}`}
      </p>
    </li>
  );
}
