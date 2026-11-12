import { ApiError, readErrorCode, readErrorFields } from './client';

/** Every request that changes data must carry this header, or the API answers 403 (decision 0014). */
const CSRF_HEADERS = { 'X-Requested-With': 'XMLHttpRequest' };

/**
 * Sends a request to the API and returns the JSON of the answer, still unchecked (`unknown`): the caller reads it
 * with the readers of `decode.ts`. Addresses are relative on purpose, like the sign-in: the app reaches the API
 * through its own address (decisions 0014 and 0027).
 */
export async function apiRequest(url: string, init: RequestInit): Promise<unknown> {
  // The cookie of the cart belongs to this address, so it goes along; a cookie never goes to another address.
  const response = await fetch(url, { credentials: 'same-origin', ...init });
  if (!response.ok) {
    // The fields are read from a copy first, so the original body is still there for the code.
    const fields = await readErrorFields(response);
    throw new ApiError(response.status, await readErrorCode(response), fields);
  }
  return response.json();
}

/**
 * An upload: the body is a form with a file. No Content-Type is set on purpose, so the browser writes it with the
 * boundary that separates the parts.
 */
export function apiUpload(url: string, form: FormData): Promise<unknown> {
  return apiRequest(url, { method: 'POST', headers: CSRF_HEADERS, body: form });
}

/** A request that changes data: JSON body and the header that protects against cross-site requests. */
export function apiSend(
  method: string,
  url: string,
  body: unknown,
  signal?: AbortSignal,
): Promise<unknown> {
  return apiRequest(url, {
    method,
    headers: { ...CSRF_HEADERS, 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
    signal,
  });
}

/** A request that changes data and has no body, such as removing a line from the cart. */
export function apiDelete(url: string, signal?: AbortSignal): Promise<unknown> {
  return apiRequest(url, { method: 'DELETE', headers: CSRF_HEADERS, signal });
}
