import { describe, expect, it } from 'vitest';
import { validateAltText, validateImageFile, validateImageUpload } from './imageValidation';

const MAX = 5 * 1024 * 1024;

function file(size: number, type: string, name = 'photo.jpg'): File {
  return new File([new Uint8Array(size)], name, { type });
}

describe('validateImageFile', () => {
  it('wants a file', () => {
    expect(validateImageFile(null)).toBe('images.error.fileRequired');
    expect(validateImageFile(undefined)).toBe('images.error.fileRequired');
  });

  it('refuses an empty file', () => {
    expect(validateImageFile(file(0, 'image/jpeg'))).toBe('images.error.fileEmpty');
  });

  it('accepts a file of exactly the maximum size and refuses one byte more', () => {
    expect(validateImageFile(file(MAX, 'image/jpeg'))).toBeUndefined();
    expect(validateImageFile(file(MAX + 1, 'image/jpeg'))).toBe('images.error.fileTooLarge');
  });

  it.each(['image/jpeg', 'image/png', 'image/webp'])('accepts %s', (type) => {
    expect(validateImageFile(file(10, type))).toBeUndefined();
  });

  it.each(['image/svg+xml', 'image/gif', 'text/plain', 'application/pdf', ''])(
    'refuses the type "%s", even for a file that is named .jpg',
    (type) => {
      expect(validateImageFile(file(10, type, 'photo.jpg'))).toBe('images.error.fileType');
    },
  );

  it('judges the size before the type', () => {
    expect(validateImageFile(file(MAX + 1, 'image/gif'))).toBe('images.error.fileTooLarge');
  });
});

describe('validateAltText', () => {
  it('wants text that is not only spaces', () => {
    expect(validateAltText('')).toBe('images.error.altRequired');
    expect(validateAltText('   ')).toBe('images.error.altRequired');
  });

  it('accepts 300 characters after trimming and refuses 301', () => {
    expect(validateAltText(` ${'а'.repeat(300)} `)).toBeUndefined();
    expect(validateAltText('а'.repeat(301))).toBe('images.error.altTooLong');
  });
});

describe('validateImageUpload', () => {
  it('reports both fields at once', () => {
    expect(validateImageUpload(null, '')).toEqual({
      file: 'images.error.fileRequired',
      altText: 'images.error.altRequired',
    });
  });

  it('is empty for a good file and text', () => {
    expect(validateImageUpload(file(10, 'image/png'), 'Дракон')).toEqual({});
  });
});
