import type { MessageKey } from '../../i18n';

/** The text fields of the form. The terms are a checkbox and are judged beside them. */
export interface CheckoutValues {
  name: string;
  email: string;
  phone: string;
  street: string;
  city: string;
  postalCode: string;
}

export type CheckoutField = keyof CheckoutValues | 'termsAccepted';

export type CheckoutErrors = Partial<Record<CheckoutField, MessageKey>>;

/** The order of the fields in the form: the first invalid one gets the focus. */
export const CHECKOUT_FIELDS: readonly CheckoutField[] = [
  'name',
  'email',
  'phone',
  'street',
  'city',
  'postalCode',
  'termsAccepted',
];

// The lengths of the columns in the database (decision 0032); the server refuses a longer text.
const MAX_LENGTH: Record<keyof CheckoutValues, number> = {
  name: 200,
  email: 254,
  phone: 30,
  street: 200,
  city: 100,
  postalCode: 20,
};

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const PHONE = /^[0-9+()\- ]{7,20}$/;
const POSTAL_CODE = /^[0-9]{4}$/;

function digitsIn(text: string): number {
  return text.replace(/[^0-9]/g, '').length;
}

/**
 * The same rules as the server, so a mistake is shown before the request. The server judges again and has the last
 * word. Values are judged trimmed, as the server does.
 */
export function validateCheckout(values: CheckoutValues, termsAccepted: boolean): CheckoutErrors {
  const errors: CheckoutErrors = {};
  for (const field of Object.keys(MAX_LENGTH) as (keyof CheckoutValues)[]) {
    const value = values[field].trim();
    if (value === '') {
      errors[field] = 'checkout.error.required';
    } else if (value.length > MAX_LENGTH[field]) {
      errors[field] = 'checkout.error.tooLong';
    }
  }
  if (errors.email === undefined && !EMAIL.test(values.email.trim())) {
    errors.email = 'checkout.error.email';
  }
  const phone = values.phone.trim();
  if (errors.phone === undefined && !(PHONE.test(phone) && digitsIn(phone) >= 7)) {
    errors.phone = 'checkout.error.phone';
  }
  if (errors.postalCode === undefined && !POSTAL_CODE.test(values.postalCode.trim())) {
    errors.postalCode = 'checkout.error.postalCode';
  }
  if (!termsAccepted) {
    errors.termsAccepted = 'checkout.error.terms';
  }
  return errors;
}
