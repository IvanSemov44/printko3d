import { describe, expect, it } from 'vitest';
import { imageUrl } from './images';

describe('imageUrl', () => {
  it('joins the base address and the storage key', () => {
    expect(imageUrl('http://img.test', 'demo/puzzles/dragon-1.jpg')).toBe(
      'http://img.test/demo/puzzles/dragon-1.jpg',
    );
  });

  it('encodes every part of the key but keeps the slashes', () => {
    expect(imageUrl('http://img.test', 'demo/my file?.jpg')).toBe(
      'http://img.test/demo/my%20file%3F.jpg',
    );
  });

  it('ignores leading and repeated slashes in the key', () => {
    expect(imageUrl('http://img.test', '/demo//a.jpg')).toBe('http://img.test/demo/a.jpg');
  });

  it('returns null without a base address', () => {
    expect(imageUrl(null, 'demo/a.jpg')).toBeNull();
  });

  it('returns null for an empty key', () => {
    expect(imageUrl('http://img.test', '')).toBeNull();
    expect(imageUrl('http://img.test', '/')).toBeNull();
  });
});
