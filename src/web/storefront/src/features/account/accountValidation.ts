import type { MessageKey } from '../../i18n';

/** The shortest password the server accepts (rule 8). The server decides; this only saves a request. */
export const PASSWORD_MIN_LENGTH = 12;

export type AccountField = 'name' | 'email' | 'password' | 'currentPassword';
export type AccountErrors = Partial<Record<AccountField, MessageKey>>;

/** An email is only checked for its shape here; the server decides the rest. */
export function looksLikeEmail(typed: string): boolean {
  return /^\S+@\S+\.\S+$/.test(typed.trim());
}

export function validateRegistration(values: {
  name: string;
  email: string;
  password: string;
}): AccountErrors {
  return {
    ...(values.name.trim() === '' ? { name: 'account.error.nameRequired' as const } : {}),
    ...(looksLikeEmail(values.email) ? {} : { email: 'account.error.emailInvalid' as const }),
    ...(values.password.length < PASSWORD_MIN_LENGTH
      ? { password: 'account.error.passwordTooShort' as const }
      : {}),
  };
}

export function validateLogin(values: { email: string; password: string }): AccountErrors {
  return {
    ...(looksLikeEmail(values.email) ? {} : { email: 'account.error.emailInvalid' as const }),
    ...(values.password === '' ? { password: 'account.error.passwordRequired' as const } : {}),
  };
}

export function validatePasswordChange(values: {
  currentPassword: string;
  password: string;
}): AccountErrors {
  return {
    ...(values.currentPassword === ''
      ? { currentPassword: 'account.error.passwordRequired' as const }
      : {}),
    ...(values.password.length < PASSWORD_MIN_LENGTH
      ? { password: 'account.error.passwordTooShort' as const }
      : {}),
  };
}

/** The first field with an error, in the order of the form, so the form can focus it. */
export function firstInvalid(
  errors: AccountErrors,
  order: readonly AccountField[],
): AccountField | undefined {
  return order.find((field) => errors[field] !== undefined);
}
