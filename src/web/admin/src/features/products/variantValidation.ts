import { parseMoneyInput, type AdminFulfilmentType, type VariantInput } from '@storefront/shared';
import type { MessageKey } from '../../i18n';
import { parseWholeNumber } from './productValidation';

export const SKU_MAX_LENGTH = 100;
export const VARIANT_NAME_MAX_LENGTH = 200;
export const MAX_ATTRIBUTES = 10;
export const ATTRIBUTE_NAME_MAX_LENGTH = 50;
export const ATTRIBUTE_VALUE_MAX_LENGTH = 100;

export interface VariantFormValues {
  sku: string;
  name: string;
  /** In the main unit, as typed: "12.50". */
  price: string;
  compareAtPrice: string;
  weightGrams: string;
  stockQuantity: string;
  /** One attribute on a line, as name=value. */
  attributes: string;
}

export type VariantFormErrors = Partial<Record<keyof VariantFormValues, MessageKey>>;

export interface VariantContext {
  fulfilmentType: AdminFulfilmentType;
  /** Decimals of the shop currency; prices are typed in the main unit and sent in the smallest one. */
  fractionDigits: number;
}

/** Reads "name=value" lines. Returns null when a line has no name or no "=", a name repeats, or a limit is passed. */
export function parseAttributes(text: string): Record<string, string> | null {
  const attributes: Record<string, string> = {};
  const lines = text.split(/\r?\n/).filter((line) => line.trim() !== '');
  if (lines.length > MAX_ATTRIBUTES) {
    return null;
  }
  for (const line of lines) {
    const separator = line.indexOf('=');
    if (separator < 0) {
      return null;
    }
    const name = line.slice(0, separator).trim();
    const value = line.slice(separator + 1).trim();
    if (
      name === '' ||
      value === '' ||
      name.length > ATTRIBUTE_NAME_MAX_LENGTH ||
      value.length > ATTRIBUTE_VALUE_MAX_LENGTH ||
      name in attributes
    ) {
      return null;
    }
    attributes[name] = value;
  }
  return attributes;
}

/** The attributes as the form shows them: one name=value on a line. */
export function formatAttributes(attributes: Readonly<Record<string, string>>): string {
  return Object.entries(attributes)
    .map(([name, value]) => `${name}=${value}`)
    .join('\n');
}

export type VariantValidation =
  { ok: true; input: VariantInput } | { ok: false; errors: VariantFormErrors };

/**
 * Checks the variant form and, when it is right, turns it into what the API takes: prices in the smallest unit,
 * whole numbers for weight and stock. The API checks the same rules again and is the real judge.
 */
export function validateVariant(
  values: VariantFormValues,
  context: VariantContext,
): VariantValidation {
  const errors: VariantFormErrors = {};

  const sku = values.sku.trim();
  if (sku === '') {
    errors.sku = 'variants.error.skuRequired';
  } else if (sku.length > SKU_MAX_LENGTH) {
    errors.sku = 'variants.error.skuTooLong';
  }

  const name = values.name.trim();
  if (name === '') {
    errors.name = 'variants.error.nameRequired';
  } else if (name.length > VARIANT_NAME_MAX_LENGTH) {
    errors.name = 'variants.error.nameTooLong';
  }

  const price = parseMoneyInput(values.price, context.fractionDigits);
  if (price === null) {
    errors.price = 'variants.error.priceInvalid';
  }

  let compareAtPrice: number | null = null;
  if (values.compareAtPrice.trim() !== '') {
    compareAtPrice = parseMoneyInput(values.compareAtPrice, context.fractionDigits);
    if (compareAtPrice === null) {
      errors.compareAtPrice = 'variants.error.compareAtInvalid';
    } else if (price !== null && compareAtPrice < price) {
      errors.compareAtPrice = 'variants.error.compareAtBelowPrice';
    }
  }

  const weightGrams = parseWholeNumber(values.weightGrams);
  if (weightGrams === null) {
    errors.weightGrams = 'variants.error.weightInvalid';
  }

  let stockQuantity: number | null = null;
  if (context.fulfilmentType === 'InStock') {
    stockQuantity = parseWholeNumber(values.stockQuantity);
    if (stockQuantity === null) {
      errors.stockQuantity = 'variants.error.stockRequired';
    }
  }

  const attributes = parseAttributes(values.attributes);
  if (attributes === null) {
    errors.attributes = 'variants.error.attributesInvalid';
  }

  if (
    Object.keys(errors).length > 0 ||
    price === null ||
    weightGrams === null ||
    attributes === null
  ) {
    return { ok: false, errors };
  }
  return {
    ok: true,
    input: { sku, name, price, compareAtPrice, stockQuantity, weightGrams, attributes },
  };
}
