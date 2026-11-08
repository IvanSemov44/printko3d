import type { ProductVariant } from '@storefront/shared';
import { useCallback } from 'react';
import { useSearchParams } from 'react-router';
import { selectVariant } from './variantSelection';

const VARIANT_PARAM = 'variant';

/**
 * The selected variant lives in the address as `?variant=<sku>`, so a link or a reload keeps it.
 * The SKU is the identifier because it is short, readable and unique. Changing it replaces the history entry.
 */
export function useSelectedVariant(variants: ProductVariant[]) {
  const [params, setParams] = useSearchParams();
  const variant = selectVariant(variants, params.get(VARIANT_PARAM));

  const select = useCallback(
    (sku: string) => {
      setParams(
        (previous) => {
          const next = new URLSearchParams(previous);
          next.set(VARIANT_PARAM, sku);
          return next;
        },
        { replace: true },
      );
    },
    [setParams],
  );

  return { variant, select };
}
