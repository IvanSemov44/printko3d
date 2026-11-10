import { describe, expect, it } from 'vitest';
import { canonicalUrl } from './canonical';
import {
  buildDescription,
  buildTitle,
  MAX_DESCRIPTION_LENGTH,
  MAX_TITLE_LENGTH,
  truncate,
} from './text';

describe('truncate', () => {
  it('leaves short text alone and collapses whitespace', () => {
    expect(truncate('  a  short\n text ', 50)).toBe('a short text');
  });

  it('cuts at a word boundary and adds an ellipsis within the limit', () => {
    const result = truncate('one two three four five', 14);
    expect(result).toBe('one two three…');
    expect(result.length).toBeLessThanOrEqual(14);
  });

  it('cuts inside a single very long word', () => {
    const result = truncate('x'.repeat(100), 10);
    expect(result).toBe(`${'x'.repeat(9)}…`);
  });
});

describe('buildTitle', () => {
  it('is the store name alone without a page name', () => {
    expect(buildTitle('Shop')).toBe('Shop');
  });

  it('joins the page and the store name', () => {
    expect(buildTitle('Shop', 'Dragon')).toBe('Dragon – Shop');
  });

  it('shortens the page name, never the store name, to stay within the limit', () => {
    const title = buildTitle('Shop', 'word '.repeat(40));
    expect(title.length).toBeLessThanOrEqual(MAX_TITLE_LENGTH);
    expect(title.endsWith('… – Shop')).toBe(true);
  });
});

describe('buildDescription', () => {
  it('stays within 160 characters', () => {
    expect(buildDescription('word '.repeat(100)).length).toBeLessThanOrEqual(
      MAX_DESCRIPTION_LENGTH,
    );
  });
});

describe('canonicalUrl', () => {
  it('is the origin and the path', () => {
    expect(canonicalUrl('https://shop.test', '/products/dragon')).toBe(
      'https://shop.test/products/dragon',
    );
  });

  it('keeps the page number only from the second page', () => {
    expect(canonicalUrl('https://shop.test', '/categories/a', 1)).toBe(
      'https://shop.test/categories/a',
    );
    expect(canonicalUrl('https://shop.test', '/categories/a', 2)).toBe(
      'https://shop.test/categories/a?page=2',
    );
  });

  it('drops a trailing slash but keeps the home page', () => {
    expect(canonicalUrl('https://shop.test', '/products/a/')).toBe('https://shop.test/products/a');
    expect(canonicalUrl('https://shop.test', '/')).toBe('https://shop.test/');
  });
});
