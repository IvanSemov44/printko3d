/**
 * Narrows an integer from the generated API types (`number | string`) to a number.
 * Throws a RangeError for anything that is not a safe integer, so a bad answer fails loudly instead of showing NaN.
 */
export function toSafeInteger(value: number | string): number {
  const number = typeof value === 'string' && /^-?\d+$/.test(value) ? Number(value) : value;
  if (typeof number !== 'number' || !Number.isSafeInteger(number)) {
    throw new RangeError('Expected a safe integer');
  }
  return number;
}
