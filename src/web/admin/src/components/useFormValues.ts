import { useState } from 'react';

/** The text values of a form, by field name. One state for all the fields instead of one `useState` for each. */
export function useFormValues<T extends Record<string, string>>(initial: T) {
  const [values, setValues] = useState<T>(initial);

  function setValue<K extends keyof T>(name: K, value: T[K]) {
    setValues((current) => ({ ...current, [name]: value }));
  }

  return { values, setValue };
}

/** Moves the focus to the field of the form with that name, if there is one. */
export function focusField(form: HTMLFormElement, name: string) {
  const element = form.elements.namedItem(name);
  if (element instanceof HTMLElement) {
    element.focus();
  }
}
