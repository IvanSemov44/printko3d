import type { ProductVariant } from '@storefront/shared';
import { useT } from '../store-settings/useStoreSettings';

/** Technical data of the selected variant, in the monospace style. */
export function ProductSpecs({ variant }: { variant: ProductVariant }) {
  const t = useT();
  return (
    <dl className="product-specs mono">
      <div>
        <dt>{t('product.sku')}</dt>
        <dd>{variant.sku}</dd>
      </div>
      <div>
        <dt>{t('product.weight')}</dt>
        <dd>{t('product.weightValue', { grams: variant.weightGrams })}</dd>
      </div>
    </dl>
  );
}
