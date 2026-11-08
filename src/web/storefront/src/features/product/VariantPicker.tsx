import type { ProductVariant } from '@storefront/shared';
import { useT } from '../store-settings/useStoreSettings';

interface VariantPickerProps {
  variants: ProductVariant[];
  selected: ProductVariant;
  onSelect: (sku: string) => void;
}

/** One variant is shown as plain text; several are a group of radio buttons. Sold-out variants stay selectable. */
export function VariantPicker({ variants, selected, onSelect }: VariantPickerProps) {
  const t = useT();

  if (variants.length === 1) {
    return <p className="variant-name">{selected.name}</p>;
  }
  return (
    <fieldset className="variant-picker">
      <legend>{t('product.variant')}</legend>
      {variants.map((variant) => (
        <label key={variant.id} className="variant-option">
          <input
            type="radio"
            name="variant"
            value={variant.sku}
            checked={variant.id === selected.id}
            onChange={() => {
              onSelect(variant.sku);
            }}
          />
          {variant.name}
          {!variant.isAvailable && ` (${t('catalog.soldOut')})`}
        </label>
      ))}
    </fieldset>
  );
}
