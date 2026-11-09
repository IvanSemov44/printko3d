import { createTranslator } from '@storefront/shared';
import bg from './locales/bg.json';

export type MessageKey = keyof typeof bg;

export const t = createTranslator<MessageKey>(bg);
