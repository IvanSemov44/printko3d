import { act, cleanup, render, renderHook, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { focusField, useFormValues } from './useFormValues';

afterEach(() => {
  cleanup();
});

describe('useFormValues', () => {
  it('starts with the initial values', () => {
    const { result } = renderHook(() => useFormValues({ name: 'Vase', slug: 'vase' }));

    expect(result.current.values).toEqual({ name: 'Vase', slug: 'vase' });
  });

  it('changes one field and keeps the others', () => {
    const { result } = renderHook(() => useFormValues({ name: 'Vase', slug: 'vase' }));

    act(() => {
      result.current.setValue('name', 'Lamp');
    });

    expect(result.current.values).toEqual({ name: 'Lamp', slug: 'vase' });
  });

  it('keeps every change when two fields change in the same update', () => {
    const { result } = renderHook(() => useFormValues({ name: '', slug: '' }));

    act(() => {
      result.current.setValue('name', 'Lamp');
      result.current.setValue('slug', 'lamp');
    });

    expect(result.current.values).toEqual({ name: 'Lamp', slug: 'lamp' });
  });
});

describe('focusField', () => {
  function renderForm() {
    render(
      <form aria-label="Product">
        <label>
          Name
          <input name="name" />
        </label>
        <label>
          Slug
          <input name="slug" />
        </label>
      </form>,
    );
    return screen.getByRole<HTMLFormElement>('form', { name: 'Product' });
  }

  it('moves the focus to the field with that name', () => {
    const form = renderForm();

    focusField(form, 'slug');

    expect(document.activeElement).toBe(screen.getByLabelText('Slug'));
  });

  it('leaves the focus where it was when there is no field with that name', () => {
    const form = renderForm();
    const name = screen.getByLabelText('Name');
    name.focus();

    expect(() => {
      focusField(form, 'missing');
    }).not.toThrow();
    expect(document.activeElement).toBe(name);
  });
});
