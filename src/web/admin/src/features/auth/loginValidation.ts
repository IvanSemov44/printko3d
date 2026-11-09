import type { MessageKey } from '../../i18n';

export interface LoginValues {
  email: string;
  password: string;
}

export type LoginErrors = Partial<Record<keyof LoginValues, MessageKey>>;

// One "@", no spaces, and a dot in the domain. The API is the real judge; this only catches typing slips.
const EMAIL_SHAPE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Checks the form before it is sent. The password is only checked for being present: its rules belong to the server. */
export function validateLogin(values: LoginValues): LoginErrors {
  const errors: LoginErrors = {};
  const email = values.email.trim();
  if (email === '') {
    errors.email = 'auth.error.emailRequired';
  } else if (!EMAIL_SHAPE.test(email)) {
    errors.email = 'auth.error.emailInvalid';
  }
  if (values.password === '') {
    errors.password = 'auth.error.passwordRequired';
  }
  return errors;
}
