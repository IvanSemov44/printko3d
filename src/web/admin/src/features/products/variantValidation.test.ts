import { describe, expect, it } from 'vitest';
import {
  formatAttributes,
  parseAttributes,
  validateVariant,
  type VariantContext,
  type VariantFormValues,
} from './variantValidation';

const VALID: VariantFormValues = {
  sku: 'DRG-1',
  name: 'Син',
  price: '12.50',
  compareAtPrice: '',
  weightGrams: '120',
  stockQuantity: '5',
  attributes: 'color=син\nsize=M',
};

const IN_STOCK: VariantContext = { fulfilmentType: 'InStock', fractionDigits: 2 };
const MADE_TO_ORDER: VariantContext = { fulfilmentType: 'MadeToOrder', fractionDigits: 2 };

function check(overrides: Partial<VariantFormValues>, context: VariantContext = IN_STOCK) {
  return validateVariant({ ...VALID, ...overrides }, context);
}

function errorsOf(overrides: Partial<VariantFormValues>, context: VariantContext = IN_STOCK) {
  const result = check(overrides, context);
  return result.ok ? {} : result.errors;
}

describe('parseAttributes', () => {
  it('reads one name=value on a line and ignores empty lines', () => {
    expect(parseAttributes('color=син\n\r\nsize = M \n')).toEqual({ color: 'син', size: 'M' });
  });

  it('keeps an equals sign inside the value', () => {
    expect(parseAttributes('formula=a=b')).toEqual({ formula: 'a=b' });
  });

  it('accepts no attributes', () => {
    expect(parseAttributes('')).toEqual({});
  });

  it.each(['color', '=син', 'color=', 'a=1\na=2'])('rejects "%s"', (text) => {
    expect(parseAttributes(text)).toBeNull();
  });

  it('accepts ten attributes and rejects eleven', () => {
    const lines = (count: number) =>
      Array.from({ length: count }, (_, index) => `k${String(index)}=v`).join('\n');
    expect(parseAttributes(lines(10))).not.toBeNull();
    expect(parseAttributes(lines(11))).toBeNull();
  });

  it('limits the length of names and values', () => {
    expect(parseAttributes(`${'k'.repeat(50)}=${'v'.repeat(100)}`)).not.toBeNull();
    expect(parseAttributes(`${'k'.repeat(51)}=v`)).toBeNull();
    expect(parseAttributes(`k=${'v'.repeat(101)}`)).toBeNull();
  });

  it('formats attributes back to lines', () => {
    expect(formatAttributes({ color: 'син', size: 'M' })).toBe('color=син\nsize=M');
    expect(parseAttributes(formatAttributes({ a: '1', b: '2' }))).toEqual({ a: '1', b: '2' });
  });
});

describe('validateVariant', () => {
  it('turns a valid form into the input of the API, with the price in the smallest unit', () => {
    expect(check({})).toEqual({
      ok: true,
      input: {
        sku: 'DRG-1',
        name: 'Син',
        price: 1250,
        compareAtPrice: null,
        stockQuantity: 5,
        weightGrams: 120,
        attributes: { color: 'син', size: 'M' },
      },
    });
  });

  it('does not lose a cent where floating point would', () => {
    const result = check({ price: '19.99', compareAtPrice: '19.99' });

    expect(result.ok && result.input.price).toBe(1999);
    expect(result.ok && result.input.compareAtPrice).toBe(1999);
  });

  it('reads a decimal comma', () => {
    const result = check({ price: '12,5' });

    expect(result.ok && result.input.price).toBe(1250);
  });

  it('trims the SKU and the name', () => {
    const result = check({ sku: '  DRG-1 ', name: ' Син ' });

    expect(result.ok && [result.input.sku, result.input.name]).toEqual(['DRG-1', 'Син']);
  });

  it('needs a SKU of at most 100 characters', () => {
    expect(errorsOf({ sku: '' }).sku).toBe('variants.error.skuRequired');
    expect(errorsOf({ sku: 'A'.repeat(100) }).sku).toBeUndefined();
    expect(errorsOf({ sku: 'A'.repeat(101) }).sku).toBe('variants.error.skuTooLong');
  });

  it('needs a name of at most 200 characters', () => {
    expect(errorsOf({ name: ' ' }).name).toBe('variants.error.nameRequired');
    expect(errorsOf({ name: 'а'.repeat(200) }).name).toBeUndefined();
    expect(errorsOf({ name: 'а'.repeat(201) }).name).toBe('variants.error.nameTooLong');
  });

  it.each(['', 'abc', '-1', '12.345', '1e3'])('rejects the price "%s"', (price) => {
    expect(errorsOf({ price }).price).toBe('variants.error.priceInvalid');
  });

  it('accepts a price of zero', () => {
    const result = check({ price: '0' });

    expect(result.ok && result.input.price).toBe(0);
  });

  it('allows no compare-at price, and rejects a bad one', () => {
    expect(errorsOf({ compareAtPrice: '' }).compareAtPrice).toBeUndefined();
    expect(errorsOf({ compareAtPrice: 'abc' }).compareAtPrice).toBe(
      'variants.error.compareAtInvalid',
    );
  });

  it('accepts a compare-at price equal to the price, and rejects a lower one', () => {
    expect(errorsOf({ price: '10', compareAtPrice: '10' }).compareAtPrice).toBeUndefined();
    expect(errorsOf({ price: '10', compareAtPrice: '10.01' }).compareAtPrice).toBeUndefined();
    expect(errorsOf({ price: '10', compareAtPrice: '9.99' }).compareAtPrice).toBe(
      'variants.error.compareAtBelowPrice',
    );
  });

  it('accepts a weight of zero and rejects a negative or fractional one', () => {
    expect(errorsOf({ weightGrams: '0' }).weightGrams).toBeUndefined();
    expect(errorsOf({ weightGrams: '-1' }).weightGrams).toBe('variants.error.weightInvalid');
    expect(errorsOf({ weightGrams: '1.5' }).weightGrams).toBe('variants.error.weightInvalid');
    expect(errorsOf({ weightGrams: '' }).weightGrams).toBe('variants.error.weightInvalid');
  });

  it('needs a stock of zero or more for an in-stock product', () => {
    expect(errorsOf({ stockQuantity: '0' }).stockQuantity).toBeUndefined();
    expect(errorsOf({ stockQuantity: '' }).stockQuantity).toBe('variants.error.stockRequired');
    expect(errorsOf({ stockQuantity: '-1' }).stockQuantity).toBe('variants.error.stockRequired');
  });

  it('sends no stock for a made-to-order product, whatever is in the field', () => {
    const result = check({ stockQuantity: 'abc' }, MADE_TO_ORDER);

    expect(result.ok && result.input.stockQuantity).toBeNull();
  });

  it('works with a currency that has no decimals', () => {
    const context: VariantContext = { fulfilmentType: 'InStock', fractionDigits: 0 };

    expect(check({ price: '500' }, context).ok).toBe(true);
    expect(errorsOf({ price: '5.5' }, context).price).toBe('variants.error.priceInvalid');
  });

  it('rejects bad attributes on their field', () => {
    expect(errorsOf({ attributes: 'color' }).attributes).toBe('variants.error.attributesInvalid');
  });

  it('reports every wrong field at once', () => {
    expect(Object.keys(errorsOf({ sku: '', name: '', price: 'x' })).sort()).toEqual([
      'name',
      'price',
      'sku',
    ]);
  });
});
