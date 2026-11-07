import type { StoreSettings } from '@storefront/shared';
import { createContext } from 'react';

export const StoreSettingsContext = createContext<StoreSettings | null>(null);
