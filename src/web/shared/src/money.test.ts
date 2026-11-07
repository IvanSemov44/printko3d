import { describe, expect, it } from 'vitest';
import { formatMoney } from './money';

/** Intl separates number and symbol with a non-breaking space; compare with a plain one. */
function plain(text: string): string {
  return text.replace(new RegExp('[\u00a0\u202f]', 'g'), ' ');
}

describe('formatMoney', () => {
  it('formats euro cents for the bg locale', () => {
    expect(plain(formatMoney(1234, 'EUR', 'bg'))).toBe('12,34 €');
  });

  it('formats the same amount differently for another locale and currency', () => {
    expect(plain(formatMoney(123456, 'USD', 'en-US'))).toBe('$1,234.56');
  });

  it('keeps amounts below one unit exact', () => {
    expect(plain(formatMoney(5, 'EUR', 'en-US'))).toBe('€0.05');
    expect(plain(formatMoney(0, 'EUR', 'en-US'))).toBe('€0.00');
  });

  it('formats negative amounts', () => {
    expect(plain(formatMoney(-250, 'EUR', 'en-US'))).toBe('-€2.50');
  });

  it('respects currencies without minor units', () => {
    expect(plain(formatMoney(500, 'JPY', 'en-US'))).toBe('¥500');
  });

  it('does not lose precision on large amounts', () => {
    expect(plain(formatMoney(900719925474099, 'EUR', 'en-US'))).toBe('€9,007,199,254,740.99');
  });

  it('falls back to bg when the locale tag is invalid', () => {
    expect(plain(formatMoney(1234, 'EUR', 'not a locale'))).toBe('12,34 €');
  });

  it('rejects amounts that are not integers', () => {
    expect(() => formatMoney(12.5, 'EUR', 'bg')).toThrow(RangeError);
  });
});
