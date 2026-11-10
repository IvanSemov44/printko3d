import { adminRequest, adminSend } from './adminHttp';
import { field, readBoolean, readInteger, readNullable, readObject, readString } from './decode';

/** The two settings the admin forms need to show and read prices. */
export interface StoreMoneySettings {
  currency: string;
  locale: string;
}

/**
 * The currency and locale of the shop. Read through the panel's own address, like the rest of the admin requests,
 * so the panel needs no separate address for the API (decision 0014).
 */
export async function getStoreMoneySettings(signal?: AbortSignal): Promise<StoreMoneySettings> {
  const object = readObject(
    await adminRequest('/api/v1/store-settings', { signal }),
    'storeSettings',
  );
  return {
    currency: field(object, 'defaultCurrency', readString, 'storeSettings'),
    locale: field(object, 'defaultLocale', readString, 'storeSettings'),
  };
}

/** What the settings form can change. The logo is set by a later task. */
export interface StoreSettingsInput {
  storeName: string;
  defaultCurrency: string;
  defaultLocale: string;
  contactEmail: string;
  contactPhone: string;
  primaryColor: string;
  isDemo: boolean;
}

/** The settings as the admin panel sees them, with the row version the form was loaded with. */
export interface AdminStoreSettings extends StoreSettingsInput {
  logoKey: string | null;
  /** Send it back with the form; a different value on the server means someone else saved first. */
  version: number;
}

const SETTINGS_PATH = '/api/v1/admin/store-settings';

function readAdminStoreSettings(value: unknown): AdminStoreSettings {
  const path = 'storeSettings';
  const object = readObject(value, path);
  return {
    storeName: field(object, 'storeName', readString, path),
    defaultCurrency: field(object, 'defaultCurrency', readString, path),
    defaultLocale: field(object, 'defaultLocale', readString, path),
    contactEmail: field(object, 'contactEmail', readString, path),
    contactPhone: field(object, 'contactPhone', readString, path),
    logoKey: field(object, 'logoKey', readNullable(readString), path),
    primaryColor: field(object, 'primaryColor', readString, path),
    isDemo: field(object, 'isDemo', readBoolean, path),
    version: field(object, 'version', readInteger, path),
  };
}

export async function getAdminStoreSettings(signal?: AbortSignal): Promise<AdminStoreSettings> {
  return readAdminStoreSettings(await adminRequest(SETTINGS_PATH, { signal }));
}

/**
 * Errors: 400 `validation.failed` (the rejected fields are in `ApiError.fields`), 409 `store_settings.version_conflict`
 * when someone saved after `version` was read, 409 `store_settings.currency_locked` when the prices forbid the currency.
 */
export async function updateStoreSettings(
  input: StoreSettingsInput,
  version: number,
): Promise<AdminStoreSettings> {
  return readAdminStoreSettings(await adminSend('PUT', SETTINGS_PATH, { ...input, version }));
}
