import type { ProductImage, ProductVariant } from '@storefront/shared';

/** The first available variant, or the first one when none is available. Variants are ordered by `sortOrder`. */
export function defaultVariant(variants: ProductVariant[]): ProductVariant | undefined {
  return variants.find((variant) => variant.isAvailable) ?? variants[0];
}

/** The variant with the given SKU; an unknown or missing SKU gives the default variant. */
export function selectVariant(
  variants: ProductVariant[],
  sku: string | null,
): ProductVariant | undefined {
  return variants.find((variant) => variant.sku === sku) ?? defaultVariant(variants);
}

/**
 * The images to show for a variant: the common ones (no variant) plus the variant's own.
 * Keeps the given order, so images ordered by `sortOrder` stay ordered.
 */
export function imagesForVariant(
  images: ProductImage[],
  variantId: string | undefined,
): ProductImage[] {
  return images.filter((image) => image.variantId === null || image.variantId === variantId);
}

/** The large image by default: the primary one when it is shown, otherwise the first. */
export function defaultImage(images: ProductImage[]): ProductImage | undefined {
  return images.find((image) => image.isPrimary) ?? images[0];
}
