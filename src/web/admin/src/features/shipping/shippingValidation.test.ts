import { describe, expect, it } from 'vitest';
import { validateTariff, type StepValues } from './shippingValidation';

const row = (key: number, weight: string, price: string): StepValues => ({ key, weight, price });

describe('validateTariff', () => {
  it('turns the typed weights and prices into steps, the price in the smallest unit', () => {
    const check = validateTariff([row(0, '1000', '6'), row(1, ' 5000 ', '9,50')], 2);

    expect(check.errors).toEqual({});
    expect(check.steps).toEqual([
      { maxWeightGrams: 1000, price: 600 },
      { maxWeightGrams: 5000, price: 950 },
    ]);
  });

  it('accepts a free step and a tariff with no step at all', () => {
    expect(validateTariff([row(0, '1000', '0')], 2).steps).toEqual([
      { maxWeightGrams: 1000, price: 0 },
    ]);
    expect(validateTariff([], 2)).toEqual({ errors: {}, steps: [] });
  });

  it.each(['', '0', '-5', '1.5', '1e3', 'abc', '1000001'])(
    'refuses the weight "%s" next to its row and sends nothing',
    (weight) => {
      const check = validateTariff([row(7, weight, '1')], 2);

      expect(check.errors[7]).toEqual({ weight: 'shipping.error.weightInvalid' });
      expect(check.steps).toBeNull();
    },
  );

  it.each(['', 'free', '-1', '1.234', '1000000000'])(
    'refuses the price "%s" next to its row',
    (price) => {
      const check = validateTariff([row(3, '1000', price)], 2);

      expect(check.errors[3]?.price).toBeDefined();
      expect(check.errors[3]?.weight).toBeUndefined();
      expect(check.steps).toBeNull();
    },
  );

  it('marks the second of two steps with the same weight, not the first', () => {
    const check = validateTariff([row(0, '1000', '6'), row(1, '1000', '7')], 2);

    expect(check.errors).toEqual({ 1: { weight: 'shipping.error.weightRepeated' } });
    expect(check.steps).toBeNull();
  });

  it('reads the price with the decimals of the currency', () => {
    expect(validateTariff([row(0, '1000', '600')], 0).steps).toEqual([
      { maxWeightGrams: 1000, price: 600 },
    ]);
    expect(validateTariff([row(0, '1000', '6.5')], 0).steps).toBeNull();
  });
});
