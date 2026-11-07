import { describe, expect, it } from 'vitest';
import {
  DEFAULT_PRIMARY_COLOR,
  contrastRatio,
  isHexColor,
  normalizePrimaryColor,
  readableTextColor,
} from './color';

describe('normalizePrimaryColor', () => {
  it('keeps a valid colour and lowercases it', () => {
    expect(normalizePrimaryColor('#2563EB')).toBe('#2563eb');
  });

  it.each(['', 'red', '#fff', '#12345', '#1234567', '2563eb', '#gggggg', 'url(x)'])(
    'uses the default for %j',
    (value) => {
      expect(isHexColor(value)).toBe(false);
      expect(normalizePrimaryColor(value)).toBe(DEFAULT_PRIMARY_COLOR);
    },
  );
});

describe('readableTextColor', () => {
  it('picks white on dark colours and black on light colours', () => {
    expect(readableTextColor('#0a1f44')).toBe('#ffffff');
    expect(readableTextColor('#ffe066')).toBe('#000000');
  });

  it('always reaches WCAG AA contrast for normal text, even on mid tones', () => {
    for (let value = 0; value <= 255; value += 5) {
      const hex = value.toString(16).padStart(2, '0');
      const background = `#${hex}${hex}${hex}`;
      expect(contrastRatio(background, readableTextColor(background))).toBeGreaterThanOrEqual(4.5);
    }
    for (const background of ['#ff0000', '#00ff00', '#0000ff', '#808080', '#767676', '#2563eb']) {
      expect(contrastRatio(background, readableTextColor(background))).toBeGreaterThanOrEqual(4.5);
    }
  });
});

describe('contrastRatio', () => {
  it('is 21 for black on white and 1 for identical colours', () => {
    expect(contrastRatio('#000000', '#ffffff')).toBeCloseTo(21);
    expect(contrastRatio('#336699', '#336699')).toBeCloseTo(1);
  });
});
