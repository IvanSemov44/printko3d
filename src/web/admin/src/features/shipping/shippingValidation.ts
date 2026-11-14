import { parseMoneyInput, type ShippingTariffStep } from '@storefront/shared';
import type { MessageKey } from '../../i18n';

/** The limits of the API (`ShippingTariffService`), which stays the judge. */
export const MAX_STEPS = 20;
export const MAX_WEIGHT_GRAMS = 1_000_000;
export const MAX_PRICE = 100_000_000;

/** What the administrator typed in one step. The key stays with the row while it is edited, so errors follow it. */
export interface StepValues {
  key: number;
  weight: string;
  price: string;
}

export interface StepErrors {
  weight?: MessageKey;
  price?: MessageKey;
}

export interface TariffCheck {
  /** The errors by the key of the row. */
  errors: Record<number, StepErrors>;
  /** The steps to send, or null when any row is wrong. */
  steps: ShippingTariffStep[] | null;
}

/** Checks every row, with the prices read in the currency that has `fractionDigits` decimals. */
export function validateTariff(rows: readonly StepValues[], fractionDigits: number): TariffCheck {
  const errors: Record<number, StepErrors> = {};
  const steps: ShippingTariffStep[] = [];
  const seen = new Set<number>();

  for (const row of rows) {
    const weightText = row.weight.trim();
    const weight = /^[0-9]+$/.test(weightText) ? Number(weightText) : null;
    const price = parseMoneyInput(row.price, fractionDigits);
    const rowErrors: StepErrors = {};

    if (weight === null || weight < 1 || weight > MAX_WEIGHT_GRAMS) {
      rowErrors.weight = 'shipping.error.weightInvalid';
    } else if (seen.has(weight)) {
      rowErrors.weight = 'shipping.error.weightRepeated';
    } else {
      seen.add(weight);
    }

    if (price === null) {
      rowErrors.price = 'shipping.error.priceInvalid';
    } else if (price > MAX_PRICE) {
      rowErrors.price = 'shipping.error.priceTooHigh';
    }

    if (rowErrors.weight !== undefined || rowErrors.price !== undefined) {
      errors[row.key] = rowErrors;
    } else if (weight !== null && price !== null) {
      steps.push({ maxWeightGrams: weight, price });
    }
  }

  return { errors, steps: Object.keys(errors).length === 0 ? steps : null };
}
