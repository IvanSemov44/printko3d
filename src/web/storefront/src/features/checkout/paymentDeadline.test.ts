import { describe, expect, it } from 'vitest';
import { formatDeadline } from './paymentDeadline';

describe('formatDeadline', () => {
  it('writes the date and the time in the language of the shop', () => {
    const text = formatDeadline('2026-11-13T11:30:00+00:00', 'bg');

    expect(text).toMatch(/13\.11\.26/);
    expect(text).toMatch(/\d{1,2}:30/);
  });

  it('gives an empty text for a date it cannot read', () => {
    expect(formatDeadline('not a date', 'bg')).toBe('');
    expect(formatDeadline('', 'bg')).toBe('');
  });
});
