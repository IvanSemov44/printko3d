/** Used when the store settings contain no valid primary colour. */
export const DEFAULT_PRIMARY_COLOR = '#f25a18';

const HEX_COLOR = /^#[0-9a-fA-F]{6}$/;

export function isHexColor(value: string): boolean {
  return HEX_COLOR.test(value);
}

/** Returns the colour in lowercase `#rrggbb` form, or the default when the value is not `#RRGGBB`. */
export function normalizePrimaryColor(value: string): string {
  return isHexColor(value) ? value.toLowerCase() : DEFAULT_PRIMARY_COLOR;
}

function channelLuminance(channel: number): number {
  const value = channel / 255;
  return value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
}

function relativeLuminance(hex: string): number {
  const red = channelLuminance(parseInt(hex.slice(1, 3), 16));
  const green = channelLuminance(parseInt(hex.slice(3, 5), 16));
  const blue = channelLuminance(parseInt(hex.slice(5, 7), 16));
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
}

/** WCAG 2.1 contrast ratio between two `#rrggbb` colours, from 1 to 21. */
export function contrastRatio(first: string, second: string): number {
  const a = relativeLuminance(first);
  const b = relativeLuminance(second);
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}

/** Picks black or white text, whichever is more readable on the background (always at least 4.5:1). */
export function readableTextColor(background: string): '#000000' | '#ffffff' {
  return contrastRatio(background, '#ffffff') >= contrastRatio(background, '#000000')
    ? '#ffffff'
    : '#000000';
}

/**
 * The primary colour is a frame and a button background, so it must stand out from white by at least 3:1
 * (WCAG 2.1, criterion 1.4.11 for components). The API checks the same limit with the same formula
 * (`ColorContrast` in Application); the tests of both use the same colours.
 */
export const MIN_PRIMARY_COLOR_CONTRAST = 3;

export function primaryColorContrast(color: string): number {
  return contrastRatio(color, '#ffffff');
}

export function hasEnoughPrimaryColorContrast(color: string): boolean {
  return isHexColor(color) && primaryColorContrast(color) >= MIN_PRIMARY_COLOR_CONTRAST;
}

function mixWithBlack(hex: string, amount: number): string {
  const channel = (start: number) =>
    Math.round(parseInt(hex.slice(start, start + 2), 16) * (1 - amount))
      .toString(16)
      .padStart(2, '0');
  return `#${channel(1)}${channel(3)}${channel(5)}`;
}

/**
 * A darker shade of the primary colour for hover and pressed states. It is darkened by up to 12 %, less when more
 * would drop the text on it (`readableTextColor` of the primary colour) below 4.5:1.
 */
export function primaryHoverColor(primary: string): string {
  const text = readableTextColor(primary);
  for (const amount of [0.12, 0.09, 0.06, 0.03]) {
    const candidate = mixWithBlack(primary, amount);
    if (contrastRatio(candidate, text) >= 4.5) {
      return candidate;
    }
  }
  return primary;
}
