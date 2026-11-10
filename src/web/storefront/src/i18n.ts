import { createTranslator, type TranslationParams } from '@storefront/shared';
import bg from './locales/bg.json';

export type MessageKey = keyof typeof bg;
export type Translate = (key: MessageKey, params?: TranslationParams) => string;

const fallback: Translate = createTranslator<MessageKey>(bg);
const translators = new Map<string, Translate>([['bg', fallback]]);

/** Texts for the given locale; a locale without translations falls back to Bulgarian. */
export function getTranslator(locale: string): Translate {
  const language = locale.split('-')[0]?.toLowerCase() ?? '';
  return translators.get(language) ?? fallback;
}

export const t: Translate = fallback;
