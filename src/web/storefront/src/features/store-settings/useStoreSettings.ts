import type { StoreSettings } from '@storefront/shared';
import { useContext, useMemo } from 'react';
import { getTranslator, type Translate } from '../../i18n';
import { StoreSettingsContext } from './StoreSettingsContext';

/** Store settings loaded at startup. Only usable below StoreSettingsProvider, which renders children once loaded. */
export function useStoreSettings(): StoreSettings {
  const settings = useContext(StoreSettingsContext);
  if (!settings) {
    throw new Error('useStoreSettings must be used inside StoreSettingsProvider');
  }
  return settings;
}

/** Translator for the locale from the store settings. */
export function useT(): Translate {
  const { defaultLocale } = useStoreSettings();
  return useMemo(() => getTranslator(defaultLocale), [defaultLocale]);
}
