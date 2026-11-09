import { describe, expect, it } from 'vitest';
import { SLUG_FORMAT, SLUG_MAX_LENGTH, slugFromName } from './slug';

describe('slugFromName', () => {
  it.each([
    ['Фигури', 'figuri'],
    ['Щампи и ножове', 'shtampi-i-nozhove'],
    ['Юнашки Ъгъл', 'yunashki-agal'],
    ['3D принтирани играчки', '3d-printirani-igrachki'],
    ['  Кутии   за --- подаръци!  ', 'kutii-za-podaratsi'],
    ['Home & Garden', 'home-garden'],
  ])('turns %j into %j', (name, slug) => {
    expect(slugFromName(name)).toBe(slug);
  });

  it('gives an empty string when nothing usable is left', () => {
    expect(slugFromName('!!! ???')).toBe('');
    expect(slugFromName('')).toBe('');
  });

  it('never ends with a hyphen after cutting to the maximum length', () => {
    const slug = slugFromName(`${'a'.repeat(SLUG_MAX_LENGTH - 1)} b`);

    expect(slug.length).toBeLessThanOrEqual(SLUG_MAX_LENGTH);
    expect(slug.endsWith('-')).toBe(false);
  });

  it('always gives something the API accepts, or nothing', () => {
    for (const name of ['Фигури', 'Щампи и ножове', 'a--b', '-x-']) {
      const slug = slugFromName(name);
      expect(slug === '' || SLUG_FORMAT.test(slug)).toBe(true);
    }
  });
});

describe('SLUG_FORMAT', () => {
  it.each(['a', 'figures', '3d-prints', 'a1-b2-c3'])('accepts %j', (slug) => {
    expect(SLUG_FORMAT.test(slug)).toBe(true);
  });

  it.each(['', 'Figures', 'two words', '-a', 'a-', 'a--b', 'фигури'])('rejects %j', (slug) => {
    expect(SLUG_FORMAT.test(slug)).toBe(false);
  });
});
