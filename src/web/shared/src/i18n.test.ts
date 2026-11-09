import { describe, expect, it } from 'vitest';
import { createTranslator } from './i18n';

describe('createTranslator', () => {
  const t = createTranslator<string>({
    'a.b': 'Текст',
    'a.name': 'Редакция на „{name}“ ({count})',
  });

  it('returns the message for a known key', () => {
    expect(t('a.b')).toBe('Текст');
  });

  it('returns the key itself when the message is missing, never an empty string', () => {
    expect(t('missing.key')).toBe('missing.key');
  });

  it('does not treat inherited object properties as messages', () => {
    expect(t('toString')).toBe('toString');
  });

  it('fills the placeholders from the parameters', () => {
    expect(t('a.name', { name: 'Фигури', count: 3 })).toBe('Редакция на „Фигури“ (3)');
  });

  it('leaves a placeholder without a parameter as it is, and ignores inherited names', () => {
    expect(t('a.name', { name: 'X' })).toBe('Редакция на „X“ ({count})');
    expect(t('a.name', {})).toBe('Редакция на „{name}“ ({count})');
  });
});
