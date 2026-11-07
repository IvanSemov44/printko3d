import { describe, expect, it } from 'vitest';
import { createTranslator } from './i18n';

describe('createTranslator', () => {
  const t = createTranslator<string>({ 'a.b': 'Текст' });

  it('returns the message for a known key', () => {
    expect(t('a.b')).toBe('Текст');
  });

  it('returns the key itself when the message is missing, never an empty string', () => {
    expect(t('missing.key')).toBe('missing.key');
  });

  it('does not treat inherited object properties as messages', () => {
    expect(t('toString')).toBe('toString');
  });
});
