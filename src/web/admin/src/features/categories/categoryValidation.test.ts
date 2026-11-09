import { describe, expect, it } from 'vitest';
import {
  DESCRIPTION_MAX_LENGTH,
  NAME_MAX_LENGTH,
  validateCategory,
  type CategoryFormValues,
} from './categoryValidation';

const VALID: CategoryFormValues = {
  name: 'Фигури',
  slug: 'figuri',
  description: '',
  sortOrder: '0',
};

describe('validateCategory', () => {
  it('accepts a valid form', () => {
    expect(validateCategory(VALID)).toEqual({});
  });

  it.each(['', '   '])('requires a name (%j)', (name) => {
    expect(validateCategory({ ...VALID, name }).name).toBe('categories.error.nameRequired');
  });

  it('limits the name length, counting without the outer spaces', () => {
    expect(validateCategory({ ...VALID, name: ` ${'a'.repeat(NAME_MAX_LENGTH)} ` })).toEqual({});
    expect(validateCategory({ ...VALID, name: 'a'.repeat(NAME_MAX_LENGTH + 1) }).name).toBe(
      'categories.error.nameTooLong',
    );
  });

  it('requires a slug', () => {
    expect(validateCategory({ ...VALID, slug: '' }).slug).toBe('categories.error.slugRequired');
  });

  it.each(['Figures', 'two words', '-a', 'a-', 'a--b', 'фигури'])('rejects the slug %j', (slug) => {
    expect(validateCategory({ ...VALID, slug }).slug).toBe('categories.error.slugInvalid');
  });

  it('limits the description length', () => {
    expect(
      validateCategory({ ...VALID, description: 'a'.repeat(DESCRIPTION_MAX_LENGTH + 1) })
        .description,
    ).toBe('categories.error.descriptionTooLong');
  });

  it.each(['', 'abc', '-1', '1.5', '1e3', '99999999999999999999'])(
    'rejects the sort order %j',
    (sortOrder) => {
      expect(validateCategory({ ...VALID, sortOrder }).sortOrder).toBe(
        'categories.error.sortOrderInvalid',
      );
    },
  );

  it('reports every problem at once, each on its own field', () => {
    const errors = validateCategory({ name: '', slug: '', description: '', sortOrder: 'x' });

    expect(Object.keys(errors).sort()).toEqual(['name', 'slug', 'sortOrder']);
  });
});
