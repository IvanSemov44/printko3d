const FALLBACK_LOCALE = 'bg';

function createFormatter(locale: string, currency: string): Intl.NumberFormat {
  const options = { style: 'currency', currency } as const;
  try {
    return new Intl.NumberFormat(locale, options);
  } catch (error) {
    if (error instanceof RangeError && locale !== FALLBACK_LOCALE) {
      // The locale tag from the settings is not valid; the currency may still be.
      return new Intl.NumberFormat(FALLBACK_LOCALE, options);
    }
    throw error;
  }
}

/**
 * Formats an amount given as an integer in the currency's smallest unit (for example euro cents).
 * The value is turned into an exact decimal string, so no floating-point arithmetic is involved.
 */
export function formatMoney(minorUnits: number, currency: string, locale: string): string {
  if (!Number.isSafeInteger(minorUnits)) {
    throw new RangeError('Money amount must be a safe integer in minor units');
  }
  const formatter = createFormatter(locale, currency);
  const fractionDigits = formatter.resolvedOptions().maximumFractionDigits ?? 2;

  const digits = String(Math.abs(minorUnits)).padStart(fractionDigits + 1, '0');
  const whole = digits.slice(0, digits.length - fractionDigits);
  const fraction = digits.slice(digits.length - fractionDigits);
  const sign = minorUnits < 0 ? '-' : '';
  const decimal = fraction === '' ? `${sign}${whole}` : `${sign}${whole}.${fraction}`;

  return formatter.format(decimal as `${number}`);
}

/** How many digits the currency has after the decimal point: 2 for euro and lev, 0 for yen. */
export function currencyFractionDigits(currency: string, locale: string): number {
  return createFormatter(locale, currency).resolvedOptions().maximumFractionDigits ?? 2;
}

/**
 * Turns what an administrator typed in the main unit ("12", "12.5", "12,50") into minor units (1250), with whole
 * numbers only: the digits are cut apart as text, nothing is multiplied as a float. Returns null for anything that
 * is not a plain non-negative amount with no more decimals than the currency has, or that is too big to be safe.
 */
export function parseMoneyInput(text: string, fractionDigits: number): number | null {
  const trimmed = text.trim();
  if (!/^[0-9]+(?:[.,][0-9]*)?$/.test(trimmed)) {
    return null;
  }
  const [whole = '', fraction = ''] = trimmed.split(/[.,]/);
  if (fraction.length > fractionDigits) {
    return null;
  }
  const minorUnits = Number(whole + fraction.padEnd(fractionDigits, '0'));
  return Number.isSafeInteger(minorUnits) ? minorUnits : null;
}

/** The reverse of `parseMoneyInput`: 1250 becomes "12.50", for filling an input. */
export function formatMoneyInput(minorUnits: number, fractionDigits: number): string {
  if (!Number.isSafeInteger(minorUnits) || minorUnits < 0) {
    throw new RangeError('Money amount must be a non-negative safe integer in minor units');
  }
  const digits = String(minorUnits).padStart(fractionDigits + 1, '0');
  const whole = digits.slice(0, digits.length - fractionDigits);
  const fraction = digits.slice(digits.length - fractionDigits);
  return fraction === '' ? whole : `${whole}.${fraction}`;
}
