import { ApiError } from './client';
import { field, readBoolean, readObject, readString } from './decode';
import { apiAct, apiRequest, apiSend } from './http';

/** The signed-in buyer (decision 0039). */
export interface Customer {
  email: string;
  displayName: string;
  /** False until the buyer opens the link in the confirmation email. */
  emailConfirmed: boolean;
  /** False for an account made only by a social sign-in. */
  hasPassword: boolean;
}

const ACCOUNT = '/api/v1/account';

/** Reads the buyer from an answer; a wrong shape throws an UnexpectedResponseError. */
export function readCustomer(value: unknown): Customer {
  const object = readObject(value, 'customer');
  return {
    email: field(object, 'email', readString, 'customer'),
    displayName: field(object, 'displayName', readString, 'customer'),
    emailConfirmed: field(object, 'emailConfirmed', readBoolean, 'customer'),
    hasPassword: field(object, 'hasPassword', readBoolean, 'customer'),
  };
}

/** The signed-in buyer, or null when there is no session (401). */
export async function getCurrentCustomer(signal?: AbortSignal): Promise<Customer | null> {
  try {
    return readCustomer(await apiRequest(`${ACCOUNT}/me`, { signal }));
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      return null;
    }
    throw error;
  }
}

/**
 * Makes an account. It does not sign the buyer in, and an address that already has an account gets the same answer.
 * A refusal is an ApiError: 400 `validation.failed` (with the fields; for the password the codes of Identity), 429.
 */
export async function registerCustomer(
  input: { email: string; password: string; name: string },
  signal?: AbortSignal,
): Promise<void> {
  await apiSend('POST', `${ACCOUNT}/register`, input, signal);
}

/**
 * Signs in. A wrong email, a wrong password and a locked account are all an ApiError with status 401, on purpose: the API
 * does not say which one it was.
 */
export async function loginCustomer(
  email: string,
  password: string,
  signal?: AbortSignal,
): Promise<Customer> {
  return readCustomer(await apiSend('POST', `${ACCOUNT}/login`, { email, password }, signal));
}

export async function logoutCustomer(signal?: AbortSignal): Promise<void> {
  await apiAct('POST', `${ACCOUNT}/logout`, signal);
}

/** A refusal is an ApiError: 400 `validation.failed` (the new password is not allowed), 422 `account.current_password_wrong`. */
export async function changeCustomerPassword(
  input: { currentPassword: string; newPassword: string },
  signal?: AbortSignal,
): Promise<void> {
  await apiSend('POST', `${ACCOUNT}/change-password`, input, signal);
}

/**
 * Confirms the address with the two values of the link in the email. A refusal is an ApiError: 422 `account.token_invalid`
 * (wrong, expired or for another address), 429.
 */
export async function confirmCustomerEmail(
  email: string,
  token: string,
  signal?: AbortSignal,
): Promise<void> {
  await apiSend('POST', `${ACCOUNT}/confirm-email`, { email, token }, signal);
}

/** Sends the confirmation email again to the signed-in buyer. Nothing is sent when the address is already confirmed. */
export async function resendCustomerConfirmation(signal?: AbortSignal): Promise<void> {
  await apiAct('POST', `${ACCOUNT}/resend-confirmation`, signal);
}

/** Asks for the link for a new password. The answer is the same for an address that has an account and one that has not. */
export async function requestPasswordReset(email: string, signal?: AbortSignal): Promise<void> {
  await apiSend('POST', `${ACCOUNT}/forgot-password`, { email }, signal);
}

/**
 * Sets a new password with the two values of the link in the email. It does not sign the buyer in. A refusal is an ApiError:
 * 400 `validation.failed` (the password is not allowed), 422 `account.token_invalid`, 429.
 */
export async function resetCustomerPassword(
  input: { email: string; token: string; newPassword: string },
  signal?: AbortSignal,
): Promise<void> {
  await apiSend('POST', `${ACCOUNT}/reset-password`, input, signal);
}
