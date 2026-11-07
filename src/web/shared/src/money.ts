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
