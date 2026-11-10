import { describe, expect, it } from 'vitest';
import { parseWholeNumber, validateProduct, type ProductFormValues } from './productValidation';

const VALID: ProductFormValues = {
  categoryId: 'c1',
  name: 'Дракон',
  slug: 'drakon',
  shortDescription: 'Кратко',
  description: 'Дълго',
  fulfilmentType: 'InStock',
  leadTimeDays: '',
  seoTitle: '',
  seoDescription: '',
};

function errorsFor(overrides: Partial<ProductFormValues>) {
  return validateProduct({ ...VALID, ...overrides });
}

describe('parseWholeNumber', () => {
  it.each([
    ['0', 0],
    ['7', 7],
    [' 12 ', 12],
  ])('reads "%s" as %i', (text, expected) => {
    expect(parseWholeNumber(text)).toBe(expected);
  });

  it.each(['', 'abc', '-1', '1.5', '1e3', '1 2', '+3', '9007199254740993'])(
    'rejects "%s"',
    (text) => {
      expect(parseWholeNumber(text)).toBeNull();
    },
  );
});

describe('validateProduct', () => {
  it('accepts a valid in-stock product', () => {
    expect(validateProduct(VALID)).toEqual({});
  });

  it('needs a category', () => {
    expect(errorsFor({ categoryId: '' }).categoryId).toBe('products.error.categoryRequired');
  });

  it.each(['', '   '])('needs a name (got "%s")', (name) => {
    expect(errorsFor({ name }).name).toBe('products.error.nameRequired');
  });

  it('accepts a name of 200 characters and rejects 201', () => {
    expect(errorsFor({ name: 'а'.repeat(200) }).name).toBeUndefined();
    expect(errorsFor({ name: 'а'.repeat(201) }).name).toBe('products.error.nameTooLong');
  });

  it('measures the name without the spaces around it', () => {
    expect(errorsFor({ name: ` ${'а'.repeat(200)} ` }).name).toBeUndefined();
  });

  it('needs a slug', () => {
    expect(errorsFor({ slug: '' }).slug).toBe('products.error.slugRequired');
  });

  it.each(['Dragon', 'two words', '-dragon', 'dragon-', 'dra--gon', 'дракон', 'dragon\n'])(
    'rejects the slug "%s"',
    (slug) => {
      expect(errorsFor({ slug }).slug).toBe('products.error.slugInvalid');
    },
  );

  it('accepts a slug of 200 characters and rejects 201', () => {
    expect(errorsFor({ slug: 'a'.repeat(200) }).slug).toBeUndefined();
    expect(errorsFor({ slug: 'a'.repeat(201) }).slug).toBe('products.error.slugInvalid');
  });

  it('accepts a short description of 200 characters and rejects 201', () => {
    expect(errorsFor({ shortDescription: 'а'.repeat(200) }).shortDescription).toBeUndefined();
    expect(errorsFor({ shortDescription: 'а'.repeat(201) }).shortDescription).toBe(
      'products.error.shortDescriptionTooLong',
    );
  });

  it('allows an empty short description and description', () => {
    expect(errorsFor({ shortDescription: '', description: '' })).toEqual({});
  });

  it('needs a lead time of at least one day for a made-to-order product', () => {
    const made = { fulfilmentType: 'MadeToOrder' as const };
    expect(errorsFor({ ...made, leadTimeDays: '' }).leadTimeDays).toBe(
      'products.error.leadTimeInvalid',
    );
    expect(errorsFor({ ...made, leadTimeDays: '0' }).leadTimeDays).toBe(
      'products.error.leadTimeInvalid',
    );
    expect(errorsFor({ ...made, leadTimeDays: '1.5' }).leadTimeDays).toBe(
      'products.error.leadTimeInvalid',
    );
    expect(errorsFor({ ...made, leadTimeDays: '1' }).leadTimeDays).toBeUndefined();
  });

  it('ignores the lead time of an in-stock product', () => {
    expect(errorsFor({ fulfilmentType: 'InStock', leadTimeDays: 'abc' })).toEqual({});
  });

  it('limits the SEO fields', () => {
    expect(errorsFor({ seoTitle: 'а'.repeat(200) }).seoTitle).toBeUndefined();
    expect(errorsFor({ seoTitle: 'а'.repeat(201) }).seoTitle).toBe(
      'products.error.seoTitleTooLong',
    );
    expect(errorsFor({ seoDescription: 'а'.repeat(500) }).seoDescription).toBeUndefined();
    expect(errorsFor({ seoDescription: 'а'.repeat(501) }).seoDescription).toBe(
      'products.error.seoDescriptionTooLong',
    );
  });

  it('reports every wrong field at once', () => {
    const errors = errorsFor({ categoryId: '', name: '', slug: 'Bad slug' });

    expect(Object.keys(errors).sort()).toEqual(['categoryId', 'name', 'slug']);
  });
});
