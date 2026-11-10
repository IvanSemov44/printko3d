import type { AdminFulfilmentType } from '@storefront/shared';
import type { MessageKey } from '../../i18n';
import { SLUG_FORMAT, SLUG_MAX_LENGTH } from '../categories/slug';

export const NAME_MAX_LENGTH = 200;
export const SHORT_DESCRIPTION_MAX_LENGTH = 200;
export const SEO_TITLE_MAX_LENGTH = 200;
export const SEO_DESCRIPTION_MAX_LENGTH = 500;

export interface ProductFormValues {
  categoryId: string;
  name: string;
  slug: string;
  shortDescription: string;
  description: string;
  fulfilmentType: AdminFulfilmentType;
  leadTimeDays: string;
  seoTitle: string;
  seoDescription: string;
}

export type ProductFormErrors = Partial<Record<keyof ProductFormValues, MessageKey>>;

/** A whole number written with digits only, or null. Spaces, signs, decimals and exponents are not accepted. */
export function parseWholeNumber(text: string): number | null {
  const trimmed = text.trim();
  if (!/^[0-9]+$/.test(trimmed)) {
    return null;
  }
  const number = Number(trimmed);
  return Number.isSafeInteger(number) ? number : null;
}

/**
 * Checks the product form before it is sent. The API checks the same rules again and is the real judge;
 * this shows the problem next to the field without a round trip.
 */
export function validateProduct(values: ProductFormValues): ProductFormErrors {
  const errors: ProductFormErrors = {};

  if (values.categoryId === '') {
    errors.categoryId = 'products.error.categoryRequired';
  }

  const name = values.name.trim();
  if (name === '') {
    errors.name = 'products.error.nameRequired';
  } else if (name.length > NAME_MAX_LENGTH) {
    errors.name = 'products.error.nameTooLong';
  }

  if (values.slug === '') {
    errors.slug = 'products.error.slugRequired';
  } else if (values.slug.length > SLUG_MAX_LENGTH || !SLUG_FORMAT.test(values.slug)) {
    errors.slug = 'products.error.slugInvalid';
  }

  if (values.shortDescription.length > SHORT_DESCRIPTION_MAX_LENGTH) {
    errors.shortDescription = 'products.error.shortDescriptionTooLong';
  }

  if (values.fulfilmentType === 'MadeToOrder') {
    const days = parseWholeNumber(values.leadTimeDays);
    if (days === null || days < 1) {
      errors.leadTimeDays = 'products.error.leadTimeInvalid';
    }
  }

  if (values.seoTitle.length > SEO_TITLE_MAX_LENGTH) {
    errors.seoTitle = 'products.error.seoTitleTooLong';
  }
  if (values.seoDescription.length > SEO_DESCRIPTION_MAX_LENGTH) {
    errors.seoDescription = 'products.error.seoDescriptionTooLong';
  }

  return errors;
}
