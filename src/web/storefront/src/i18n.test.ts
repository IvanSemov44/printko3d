import { describe, expect, it } from 'vitest';
import { getTranslator } from './i18n';

describe('getTranslator', () => {
  it('returns Bulgarian texts for bg and regional variants', () => {
    expect(getTranslator('bg')('settings.retry')).toBe('Опитай пак');
    expect(getTranslator('bg-BG')('settings.retry')).toBe('Опитай пак');
  });

  it('falls back to Bulgarian for a locale without translations', () => {
    expect(getTranslator('de-DE')('settings.retry')).toBe('Опитай пак');
    expect(getTranslator('')('settings.retry')).toBe('Опитай пак');
  });
});
