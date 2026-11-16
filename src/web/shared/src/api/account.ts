import { ApiError } from './client';
import {
  field,
  readArray,
  readBoolean,
  readInteger,
  readObject,
  readOneOf,
  readString,
  type Reader,
} from './decode';
import { apiAct, apiRequest, apiSend } from './http';
import { ORDER_STATUSES, readOrder, type Order, type OrderStatus } from './orders';

/** The signed-in buyer (decision 0039). */
export interface Customer {
  email: string;
  displayName: string;
  /** False until the buyer opens the link in the confirmation email. */
  emailConfirmed: boolean;
  /** False for an account made only by a social sign-in. */
  hasPassword: boolean;
  /** The social providers connected to the account, such as `Google`. */
  linkedProviders: string[];
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
    linkedProviders: field(object, 'linkedProviders', readArray(readString), 'customer'),
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

/** One row of the history of orders of a buyer. Money is in the smallest unit of `currency`. */
export interface CustomerOrderListItem {
  number: number;
  status: OrderStatus;
  currency: string;
  total: number;
  itemCount: number;
  createdAt: string;
}

export interface CustomerOrderPage {
  items: CustomerOrderListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

const readListItem: Reader<CustomerOrderListItem> = (value, path) => {
  const object = readObject(value, path);
  return {
    number: field(object, 'number', readInteger, path),
    status: field(object, 'status', readOneOf(ORDER_STATUSES), path),
    currency: field(object, 'currency', readString, path),
    total: field(object, 'total', readInteger, path),
    itemCount: field(object, 'itemCount', readInteger, path),
    createdAt: field(object, 'createdAt', readString, path),
  };
};

function readOrderPage(value: unknown): CustomerOrderPage {
  const path = 'customerOrders';
  const object = readObject(value, path);
  const pageSize = field(object, 'pageSize', readInteger, path);
  const totalCount = field(object, 'totalCount', readInteger, path);
  return {
    items: field(object, 'items', readArray(readListItem), path),
    page: field(object, 'page', readInteger, path),
    pageSize,
    totalCount,
    // The server may leave it out; it is then worked out from the other two numbers.
    totalPages:
      object.totalPages === undefined
        ? Math.ceil(totalCount / pageSize)
        : field(object, 'totalPages', readInteger, path),
  };
}

/** The orders of the signed-in buyer, the newest first, one page at a time. A refusal is an ApiError: 400 `validation.failed`, 401. */
export async function getMyOrders(
  query: { page: number; pageSize: number },
  signal?: AbortSignal,
): Promise<CustomerOrderPage> {
  const params = new URLSearchParams({
    page: String(query.page),
    pageSize: String(query.pageSize),
  });
  return readOrderPage(await apiRequest(`${ACCOUNT}/orders?${params.toString()}`, { signal }));
}

/**
 * One order of the signed-in buyer. A refusal is an ApiError: 401, 404 `order.not_found` (the same for a number that does not
 * exist and one that is another buyer's).
 */
export async function getMyOrder(number: number, signal?: AbortSignal): Promise<Order> {
  return readOrder(await apiRequest(`${ACCOUNT}/orders/${String(number)}`, { signal }));
}

/** The social providers that are turned on in this environment (`Google`, `Facebook`), in the order the buttons are shown. Empty when none is. */
export async function getExternalProviders(signal?: AbortSignal): Promise<string[]> {
  const object = readObject(
    await apiRequest(`${ACCOUNT}/external/providers`, { signal }),
    'externalProviders',
  );
  return field(object, 'providers', readArray(readString), 'externalProviders');
}

/**
 * The address that sends the browser to a provider. It is opened by navigating (a link), not by a request: the provider asks the buyer on
 * its own page and sends the browser back to the shop.
 */
export function externalSignInAddress(provider: string): string {
  return `${ACCOUNT}/external/${encodeURIComponent(provider)}/start`;
}

/**
 * Asks to connect a provider to the signed-in account. The answer is a short cookie that the connecting needs, so another site cannot do
 * it in the buyer's name. After it the browser is sent to `externalLinkAddress`. A refusal is an ApiError: 401, 404.
 */
export async function prepareExternalLink(provider: string, signal?: AbortSignal): Promise<void> {
  await apiAct('POST', `${ACCOUNT}/external/${encodeURIComponent(provider)}/link`, signal);
}

/** The address that sends the browser to a provider to connect it to the signed-in account (after `prepareExternalLink`). */
export function externalLinkAddress(provider: string): string {
  return `${externalSignInAddress(provider)}?mode=link`;
}
