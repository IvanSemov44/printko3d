import { describe, expect, it } from 'vitest';
import { toSafeInteger } from './numbers';

describe('toSafeInteger', () => {
  it('returns an integer number as it is', () => {
    expect(toSafeInteger(1250)).toBe(1250);
    expect(toSafeInteger(-3)).toBe(-3);
    expect(toSafeInteger(0)).toBe(0);
  });

  it('converts an integer written as a string', () => {
    expect(toSafeInteger('1250')).toBe(1250);
    expect(toSafeInteger('-3')).toBe(-3);
  });

  it.each([1.5, Number.NaN, Number.POSITIVE_INFINITY, 2 ** 53])(
    'rejects the number %s',
    (value) => {
      expect(() => toSafeInteger(value)).toThrow(RangeError);
    },
  );

  it.each(['', ' 5', '5 ', '1.5', '1e3', 'abc', '0x10', '9007199254740993'])(
    'rejects the string %j',
    (value) => {
      expect(() => toSafeInteger(value)).toThrow(RangeError);
    },
  );
});
