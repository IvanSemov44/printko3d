import type { MessageKey } from '../../i18n';
import { SLUG_FORMAT, SLUG_MAX_LENGTH } from './slug';

export const NAME_MAX_LENGTH = 200;
export const DESCRIPTION_MAX_LENGTH = 2000;

export interface CategoryFormValues {
  name: string;
  slug: string;
  description: string;
  sortOrder: string;
}

export type CategoryFormErrors = Partial<Record<keyof CategoryFormValues, MessageKey>>;

/**
 * Checks the form before it is sent. The API checks the same rules again and is the real judge;
 * this shows the problem next to the field without a round trip.
 */
export function validateCategory(values: CategoryFormValues): CategoryFormErrors {
  const errors: CategoryFormErrors = {};

  const name = values.name.trim();
  if (name === '') {
    errors.name = 'categories.error.nameRequired';
  } else if (name.length > NAME_MAX_LENGTH) {
    errors.name = 'categories.error.nameTooLong';
  }

  if (values.slug === '') {
    errors.slug = 'categories.error.slugRequired';
  } else if (values.slug.length > SLUG_MAX_LENGTH || !SLUG_FORMAT.test(values.slug)) {
    errors.slug = 'categories.error.slugInvalid';
  }

  if (values.description.length > DESCRIPTION_MAX_LENGTH) {
    errors.description = 'categories.error.descriptionTooLong';
  }

  const sortOrder = values.sortOrder.trim();
  if (!/^\d+$/.test(sortOrder) || !Number.isSafeInteger(Number(sortOrder))) {
    errors.sortOrder = 'categories.error.sortOrderInvalid';
  }

  return errors;
}
