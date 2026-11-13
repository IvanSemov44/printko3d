import type { ReactNode } from 'react';

/**
 * What a control needs to be tied to its label, hint and error. Spread it on the input, select or textarea.
 * This is the second copy of the admin panel's `FormField` (debt D66): it moves to `@storefront/shared` when a third
 * place needs it, or with D25 and D26.
 */
export interface FieldControlProps {
  id: string;
  name: string;
  'aria-invalid': true | undefined;
  'aria-describedby': string | undefined;
}

interface FormFieldProps {
  /** The id of the control. The hint and the error get `${id}-hint` and `${id}-error`. */
  id: string;
  /** The name of the control: the form focuses the first invalid field by it. */
  name: string;
  label: string;
  hint?: string | undefined;
  /** Already translated. When it is set, the control is marked invalid and the message is read with it. */
  error?: string | undefined;
  children: (control: FieldControlProps) => ReactNode;
}

/** A label, a control, an optional hint and an optional error under it, all tied to the control for a screen reader. */
export function FormField({ id, name, label, hint, error, children }: FormFieldProps) {
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const describedBy = [hint ? hintId : null, error ? errorId : null].filter(Boolean).join(' ');

  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      {children({
        id,
        name,
        'aria-invalid': error ? true : undefined,
        'aria-describedby': describedBy === '' ? undefined : describedBy,
      })}
      {hint ? (
        <p id={hintId} className="field-hint">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={errorId} className="field-error">
          {error}
        </p>
      ) : null}
    </div>
  );
}
