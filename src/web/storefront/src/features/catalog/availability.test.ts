import { describe, expect, it } from 'vitest';
import { getTranslator } from '../../i18n';
import { describeAvailability } from './availability';

const t = getTranslator('bg');
const describe_ = (
  fulfilmentType: 'InStock' | 'MadeToOrder',
  isAvailable: boolean,
  leadTimeDays: number | null,
) => describeAvailability(t, 'bg', { fulfilmentType, isAvailable, leadTimeDays });

describe('describeAvailability', () => {
  it('is "in stock" for an available stock item', () => {
    expect(describe_('InStock', true, null)).toBe('В наличност');
  });

  it('is "sold out" for a stock item that is not available', () => {
    expect(describe_('InStock', false, null)).toBe('Изчерпан');
  });

  it('ignores a lead time on a stock item', () => {
    expect(describe_('InStock', true, 5)).toBe('В наличност');
  });

  it('is "made to order" with the lead time in days', () => {
    expect(describe_('MadeToOrder', true, 5)).toBe('По поръчка · 5 дни');
    expect(describe_('MadeToOrder', true, 1)).toBe('По поръчка · 1 ден');
  });

  it('is "made to order" without a lead time when none is set', () => {
    expect(describe_('MadeToOrder', true, null)).toBe('По поръчка');
  });

  it('is never "sold out" for a made-to-order item', () => {
    expect(describe_('MadeToOrder', false, null)).toBe('По поръчка');
  });
});
