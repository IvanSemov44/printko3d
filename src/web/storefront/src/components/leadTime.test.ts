import { describe, expect, it } from 'vitest';
import { formatLeadTime } from './leadTime';

describe('formatLeadTime', () => {
  it('uses the singular for one day', () => {
    expect(formatLeadTime('bg', 1)).toBe('1 ден');
  });

  it.each([0, 2, 5, 11, 21])('uses the plural for %i days', (days) => {
    expect(formatLeadTime('bg', days)).toBe(`${String(days)} дни`);
  });

  it('keeps working for an invalid locale tag', () => {
    expect(formatLeadTime('not a locale', 3)).toBe('3 дни');
  });
});
