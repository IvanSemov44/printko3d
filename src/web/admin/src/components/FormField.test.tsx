import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { FormField } from './FormField';

function renderField(props: { hint?: string; error?: string } = {}) {
  return render(
    <FormField id="demo" name="demo" label="Име" {...props}>
      {(control) => <input {...control} type="text" />}
    </FormField>,
  );
}

describe('FormField', () => {
  it('ties the label to the control', () => {
    renderField();

    expect(screen.getByLabelText('Име')).toHaveAttribute('id', 'demo');
    expect(screen.getByLabelText('Име')).toHaveAttribute('name', 'demo');
  });

  it('has no description and is not invalid without a hint or an error', () => {
    renderField();

    expect(screen.getByLabelText('Име')).not.toHaveAttribute('aria-describedby');
    expect(screen.getByLabelText('Име')).not.toHaveAttribute('aria-invalid');
    expect(screen.getByLabelText('Име')).toBeValid();
  });

  it('describes the control with the hint', () => {
    renderField({ hint: 'Само букви.' });

    expect(screen.getByLabelText('Име')).toHaveAccessibleDescription('Само букви.');
    expect(screen.getByLabelText('Име')).toBeValid();
  });

  it('marks the control invalid and describes it with the error', () => {
    renderField({ error: 'Въведи име.' });

    expect(screen.getByLabelText('Име')).toBeInvalid();
    expect(screen.getByLabelText('Име')).toHaveAccessibleDescription('Въведи име.');
    expect(screen.getByText('Въведи име.')).toHaveClass('field-error');
  });

  it('reads the hint and then the error', () => {
    renderField({ hint: 'Само букви.', error: 'Въведи име.' });

    expect(screen.getByLabelText('Име')).toHaveAccessibleDescription('Само букви. Въведи име.');
  });
});
