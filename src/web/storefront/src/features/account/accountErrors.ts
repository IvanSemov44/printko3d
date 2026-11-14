import { ApiError } from '@storefront/shared';
import type { MessageKey } from '../../i18n';

/** The text for a refused sign-in. A wrong email, a wrong password and a locked account look the same on purpose (decision 0039). */
export function loginFailure(error: unknown): MessageKey {
  if (!(error instanceof ApiError)) {
    return 'account.error.generic';
  }
  if (error.status === 401) {
    return 'account.login.error.invalid';
  }
  return error.status === 429 ? 'account.error.tooMany' : 'account.error.generic';
}

/** The text for a refused registration or a refused new password. */
export function passwordFailure(error: unknown): MessageKey {
  if (!(error instanceof ApiError)) {
    return 'account.error.generic';
  }
  if (error.status === 429) {
    return 'account.error.tooMany';
  }
  if (error.code === 'account.current_password_wrong') {
    return 'account.error.currentPasswordWrong';
  }
  if (error.code === 'validation.failed') {
    return error.fields.includes('email')
      ? 'account.error.emailInvalid'
      : 'account.error.passwordRejected';
  }
  return 'account.error.generic';
}
