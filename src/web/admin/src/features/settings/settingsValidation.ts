import { hasEnoughPrimaryColorContrast, isHexColor } from '@storefront/shared';
import type { MessageKey } from '../../i18n';

export const NAME_MAX_LENGTH = 200;
export const EMAIL_MAX_LENGTH = 320;
export const PHONE_MAX_LENGTH = 50;

/**
 * The choices of the form. They repeat the list of the API (`StoreSettingsRules`), which stays the judge: a value
 * the API does not allow is shown next to its field after the save.
 */
export const CURRENCIES = ['EUR', 'BGN'] as const;
export const LOCALES = ['bg'] as const;

const EMAIL_FORMAT = /^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$/;
const PHONE_FORMAT = /^[0-9 +\-()]+$/;

export interface SettingsFormValues {
  storeName: string;
  defaultCurrency: string;
  defaultLocale: string;
  contactEmail: string;
  contactPhone: string;
  primaryColor: string;
}

export type SettingsFieldName = keyof SettingsFormValues;
export type SettingsFormErrors = Partial<Record<SettingsFieldName, MessageKey>>;

/** The order of the fields on the screen: the first invalid one gets the focus. */
export const SETTINGS_FIELD_ORDER: readonly SettingsFieldName[] = [
  'storeName',
  'defaultCurrency',
  'defaultLocale',
  'contactEmail',
  'contactPhone',
  'primaryColor',
];

/** What to say about a field the API rejected, when the browser had no objection. */
export const SERVER_REJECTED_KEYS: Record<SettingsFieldName, MessageKey> = {
  storeName: 'settings.error.serverRejected.storeName',
  defaultCurrency: 'settings.error.serverRejected.defaultCurrency',
  defaultLocale: 'settings.error.serverRejected.defaultLocale',
  contactEmail: 'settings.error.serverRejected.contactEmail',
  contactPhone: 'settings.error.serverRejected.contactPhone',
  primaryColor: 'settings.error.serverRejected.primaryColor',
};

export function isSettingsField(name: string): name is SettingsFieldName {
  return Object.hasOwn(SERVER_REJECTED_KEYS, name);
}

/**
 * Checks the form before it is sent. The API checks the same rules again and is the real judge;
 * this shows the problem next to the field without a round trip.
 */
export function validateSettings(values: SettingsFormValues): SettingsFormErrors {
  const errors: SettingsFormErrors = {};

  const name = values.storeName.trim();
  if (name === '') {
    errors.storeName = 'settings.error.nameRequired';
  } else if (name.length > NAME_MAX_LENGTH) {
    errors.storeName = 'settings.error.nameTooLong';
  }

  if (!(CURRENCIES as readonly string[]).includes(values.defaultCurrency)) {
    errors.defaultCurrency = 'settings.error.currencyInvalid';
  }

  if (!(LOCALES as readonly string[]).includes(values.defaultLocale)) {
    errors.defaultLocale = 'settings.error.localeInvalid';
  }

  if (values.contactEmail === '') {
    errors.contactEmail = 'settings.error.emailRequired';
  } else if (values.contactEmail.length > EMAIL_MAX_LENGTH) {
    errors.contactEmail = 'settings.error.emailTooLong';
  } else if (!EMAIL_FORMAT.test(values.contactEmail)) {
    errors.contactEmail = 'settings.error.emailInvalid';
  }

  const phone = values.contactPhone.trim();
  if (phone.length > PHONE_MAX_LENGTH) {
    errors.contactPhone = 'settings.error.phoneTooLong';
  } else if (phone !== '' && !PHONE_FORMAT.test(phone)) {
    errors.contactPhone = 'settings.error.phoneInvalid';
  }

  if (!isHexColor(values.primaryColor)) {
    errors.primaryColor = 'settings.error.colorInvalid';
  } else if (!hasEnoughPrimaryColorContrast(values.primaryColor)) {
    errors.primaryColor = 'settings.error.colorLowContrast';
  }

  return errors;
}
