import type { ProductImage, ProductVariant } from '@storefront/shared';
import { describe, expect, it } from 'vitest';
import { defaultImage, defaultVariant, imagesForVariant, selectVariant } from './variantSelection';

function variant(sku: string, isAvailable = true): ProductVariant {
  return {
    id: `id-${sku}`,
    sku,
    name: sku,
    price: 100,
    compareAtPrice: null,
    isAvailable,
    weightGrams: 10,
    attributes: {},
    sortOrder: 0,
  };
}

function image(id: string, variantId: string | null = null, isPrimary = false): ProductImage {
  return { id, variantId, storageKey: id, altText: id, sortOrder: 0, isPrimary, url: null };
}

describe('defaultVariant', () => {
  it('is the first available variant', () => {
    expect(defaultVariant([variant('A', false), variant('B'), variant('C')])?.sku).toBe('B');
  });

  it('is the first variant when none is available', () => {
    expect(defaultVariant([variant('A', false), variant('B', false)])?.sku).toBe('A');
  });

  it('is undefined without variants', () => {
    expect(defaultVariant([])).toBeUndefined();
  });
});

describe('selectVariant', () => {
  const variants = [variant('A'), variant('B', false)];

  it('finds the variant by SKU, even when it is sold out', () => {
    expect(selectVariant(variants, 'B')?.sku).toBe('B');
  });

  it.each([[null], ['nope'], ['']])('falls back to the default for %j', (sku) => {
    expect(selectVariant(variants, sku)?.sku).toBe('A');
  });
});

describe('imagesForVariant', () => {
  const images = [image('common'), image('a', 'id-A'), image('b', 'id-B')];

  it('shows the common images and the variant’s own', () => {
    expect(imagesForVariant(images, 'id-A').map((i) => i.id)).toEqual(['common', 'a']);
  });

  it('shows only the common images for a variant without its own', () => {
    expect(imagesForVariant(images, 'id-C').map((i) => i.id)).toEqual(['common']);
  });

  it('shows the common images when there is no variant', () => {
    expect(imagesForVariant(images, undefined).map((i) => i.id)).toEqual(['common']);
  });
});

describe('defaultImage', () => {
  it('is the primary image', () => {
    expect(defaultImage([image('a'), image('b', null, true)])?.id).toBe('b');
  });

  it('is the first image when the primary one is not shown', () => {
    expect(defaultImage([image('a'), image('b')])?.id).toBe('a');
  });

  it('is undefined without images', () => {
    expect(defaultImage([])).toBeUndefined();
  });
});
