import { IMAGE_ALT_MAX_LENGTH, IMAGE_MAX_BYTES, IMAGE_TYPES } from '@storefront/shared';
import type { MessageKey } from '../../i18n';

export type ImageFormErrors = Partial<Record<'file' | 'altText', MessageKey>>;

/**
 * Checks the file before it is sent, so the administrator does not wait for a large upload that will be refused.
 * The browser knows the type only from the name of the file, so this is a convenience: the server decides the type
 * from the content of the file and has the last word.
 */
export function validateImageFile(file: File | null | undefined): MessageKey | undefined {
  if (!file) {
    return 'images.error.fileRequired';
  }
  if (file.size === 0) {
    return 'images.error.fileEmpty';
  }
  if (file.size > IMAGE_MAX_BYTES) {
    return 'images.error.fileTooLarge';
  }
  if (!(IMAGE_TYPES as readonly string[]).includes(file.type)) {
    return 'images.error.fileType';
  }
  return undefined;
}

export function validateAltText(altText: string): MessageKey | undefined {
  const trimmed = altText.trim();
  if (trimmed === '') {
    return 'images.error.altRequired';
  }
  return trimmed.length > IMAGE_ALT_MAX_LENGTH ? 'images.error.altTooLong' : undefined;
}

export function validateImageUpload(
  file: File | null | undefined,
  altText: string,
): ImageFormErrors {
  const errors: ImageFormErrors = {};
  const fileError = validateImageFile(file);
  if (fileError) {
    errors.file = fileError;
  }
  const altError = validateAltText(altText);
  if (altError) {
    errors.altText = altError;
  }
  return errors;
}
