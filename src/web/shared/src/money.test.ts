import { describe, expect, it } from 'vitest';
import { currencyFractionDigits, formatMoney, formatMoneyInput, parseMoneyInput } from './money';

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

describe('money input', () => {
  it('knows how many decimals a currency has', () => {
    expect(currencyFractionDigits('EUR', 'bg')).toBe(2);
    expect(currencyFractionDigits('JPY', 'en')).toBe(0);
  });

  it.each([
    ['12', 2, 1200],
    ['12.5', 2, 1250],
    ['12,50', 2, 1250],
    ['0', 2, 0],
    ['0.01', 2, 1],
    ['12.', 2, 1200],
    [' 7 ', 2, 700],
    ['500', 0, 500],
  ])('reads "%s" with %i decimals as %i', (text, digits, expected) => {
    expect(parseMoneyInput(text, digits)).toBe(expected);
  });

  it.each([
    ['', 2],
    ['abc', 2],
    ['-1', 2],
    ['12.345', 2],
    ['1 2', 2],
    ['1e3', 2],
    ['12.5', 0],
    ['.5', 2],
    ['9007199254740993', 2],
  ])('rejects "%s" with %i decimals', (text, digits) => {
    expect(parseMoneyInput(text, digits)).toBeNull();
  });

  it('does not lose a cent where floating point would', () => {
    // 0.1 + 0.2 style trouble: 19.99 * 100 is 1998.9999999999998 as a float.
    expect(parseMoneyInput('19.99', 2)).toBe(1999);
    expect(parseMoneyInput('1.15', 2)).toBe(115);
  });

  it.each([
    [1250, 2, '12.50'],
    [5, 2, '0.05'],
    [0, 2, '0.00'],
    [500, 0, '500'],
  ])('formats %i minor units with %i decimals as "%s"', (minor, digits, expected) => {
    expect(formatMoneyInput(minor, digits)).toBe(expected);
  });

  it('formats and parses back to the same amount', () => {
    for (const minor of [0, 1, 99, 100, 1999, 123456789]) {
      expect(parseMoneyInput(formatMoneyInput(minor, 2), 2)).toBe(minor);
    }
  });

  it('refuses a negative or fractional amount of minor units', () => {
    expect(() => formatMoneyInput(-1, 2)).toThrow(RangeError);
    expect(() => formatMoneyInput(1.5, 2)).toThrow(RangeError);
  });
});
