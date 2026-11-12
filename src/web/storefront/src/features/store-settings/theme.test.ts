import { contrastRatio } from '@storefront/shared';
import { describe, expect, it } from 'vitest';
import { applyTheme } from './theme';

function themeOf(primaryColor: string) {
  const root = document.createElement('div');
  applyTheme(primaryColor, root);
  const read = (name: string) => root.style.getPropertyValue(name);
  return {
    primary: read('--color-primary'),
    hover: read('--color-primary-hover'),
    onPrimary: read('--color-on-primary'),
  };
}

describe('applyTheme', () => {
  it('uses the colour from the settings as the primary colour', () => {
    expect(themeOf('#2F5D8A').primary).toBe('#2f5d8a');
  });

  it.each(['#0a1f44', '#ffe066', '#f25a18', '#767676', '#00ff00'])(
    'keeps the text readable on %s, normally and on hover',
    (color) => {
      const { primary, hover, onPrimary } = themeOf(color);
      expect(contrastRatio(primary, onPrimary)).toBeGreaterThanOrEqual(4.5);
      expect(contrastRatio(hover, onPrimary)).toBeGreaterThanOrEqual(4.5);
    },
  );

  it('computes the hover colour from the primary colour instead of using a fixed one', () => {
    expect(themeOf('#0a1f44').hover).not.toBe(themeOf('#f25a18').hover);
    expect(themeOf('#f25a18').hover).not.toBe('#f25a18');
  });

  it('falls back to the default colour when the colour is invalid', () => {
    const fallback = themeOf('not-a-colour');
    expect(fallback.primary).toBe('#f25a18');
    expect(fallback.onPrimary).toBe('#000000');
    expect(fallback.hover).toBe(themeOf('#f25a18').hover);
  });
});
