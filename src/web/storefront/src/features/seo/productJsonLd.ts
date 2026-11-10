import { currencyFractionDigits, formatMoneyInput, type ProductDetails } from '@storefront/shared';
import { availabilityKind, type AvailabilityKind } from '../catalog/availability';
import { defaultImage } from '../product/variantSelection';

const SCHEMA_AVAILABILITY: Record<AvailabilityKind, string> = {
  inStock: 'https://schema.org/InStock',
  soldOut: 'https://schema.org/OutOfStock',
  madeToOrder: 'https://schema.org/PreOrder',
};

/**
 * schema.org `Product` for search engines, with the cheapest variant as the offer. It holds only what the page shows:
 * the price is in the store's currency, the availability follows the same rule as the page.
 */
export function buildProductJsonLd(
  product: ProductDetails,
  options: { currency: string; locale: string; url: string },
): Record<string, unknown> | null {
  const cheapest = product.variants.reduce<ProductDetails['variants'][number] | undefined>(
    (best, variant) => (best === undefined || variant.price < best.price ? variant : best),
    undefined,
  );
  if (cheapest === undefined) {
    return null;
  }
  const image = defaultImage(product.images)?.url ?? null;
  const description = product.description !== '' ? product.description : product.shortDescription;
  const fractionDigits = currencyFractionDigits(options.currency, options.locale);

  return {
    '@context': 'https://schema.org',
    '@type': 'Product',
    name: product.name,
    ...(description !== '' && { description }),
    ...(image !== null && { image }),
    sku: cheapest.sku,
    offers: {
      '@type': 'Offer',
      url: options.url,
      price: formatMoneyInput(cheapest.price, fractionDigits),
      priceCurrency: options.currency,
      availability:
        SCHEMA_AVAILABILITY[
          availabilityKind({
            fulfilmentType: product.fulfilmentType,
            isAvailable: cheapest.isAvailable,
          })
        ],
    },
  };
}

/** JSON for a `<script>` element: `<` is escaped so text in the data can never close the element. */
export function serializeJsonLd(data: Record<string, unknown>): string {
  return JSON.stringify(data).replace(/</g, '\u003c');
}
