import { adminRequest } from './adminHttp';
import { field, readObject, readString } from './decode';

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
