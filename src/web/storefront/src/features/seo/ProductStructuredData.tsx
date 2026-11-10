import type { ProductDetails } from '@storefront/shared';
import { useLocation } from 'react-router';
import { useStoreSettings } from '../store-settings/useStoreSettings';
import { canonicalUrl } from './canonical';
import { buildProductJsonLd, serializeJsonLd } from './productJsonLd';

/** Structured data for search engines. A data block, not a script: the Content-Security-Policy does not touch it. */
export function ProductStructuredData({ product }: { product: ProductDetails }) {
  const { defaultCurrency, defaultLocale } = useStoreSettings();
  const { pathname } = useLocation();
  const data = buildProductJsonLd(product, {
    currency: defaultCurrency,
    locale: defaultLocale,
    url: canonicalUrl(window.location.origin, pathname),
  });
  if (data === null) {
    return null;
  }
  return <script type="application/ld+json">{serializeJsonLd(data)}</script>;
}
