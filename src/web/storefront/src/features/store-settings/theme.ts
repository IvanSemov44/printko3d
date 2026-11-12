import { normalizePrimaryColor, primaryHoverColor, readableTextColor } from '@storefront/shared';

/** Writes the theme colours as CSS variables; an invalid colour falls back to the default. */
export function applyTheme(primaryColor: string, root: HTMLElement = document.documentElement) {
  const primary = normalizePrimaryColor(primaryColor);
  root.style.setProperty('--color-primary', primary);
  root.style.setProperty('--color-primary-hover', primaryHoverColor(primary));
  root.style.setProperty('--color-on-primary', readableTextColor(primary));
}
