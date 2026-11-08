import { describe, expect, it } from 'vitest';
import { getPageItems, parsePageParam } from './pageParams';

describe('parsePageParam', () => {
  it('reads a whole number', () => {
    expect(parsePageParam('2')).toBe(2);
    expect(parsePageParam('15')).toBe(15);
  });

  it.each([null, '', 'abc', '0', '-1', '1.5', '2x', ' 2', '99999999999999999999'])(
    'falls back to the first page for %j',
    (raw) => {
      expect(parsePageParam(raw)).toBe(1);
    },
  );
});

describe('getPageItems', () => {
  it('lists every page when there are few', () => {
    expect(getPageItems(2, 4)).toEqual([1, 2, 3, 4]);
    expect(getPageItems(1, 2)).toEqual([1, 2]);
  });

  it('puts a gap where several pages are skipped', () => {
    expect(getPageItems(1, 10)).toEqual([1, 2, 'gap', 10]);
    expect(getPageItems(5, 10)).toEqual([1, 'gap', 4, 5, 6, 'gap', 10]);
    expect(getPageItems(10, 10)).toEqual([1, 'gap', 9, 10]);
  });

  it('shows the page itself instead of a gap that hides only one page', () => {
    expect(getPageItems(4, 10)).toEqual([1, 2, 3, 4, 5, 'gap', 10]);
  });
});
