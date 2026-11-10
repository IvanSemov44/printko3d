import { ApiError, readErrorCode, readErrorFields } from './client';

/** Every request that changes data must carry this header, or the API answers 403 (decision 0014). */
const CSRF_HEADERS = { 'X-Requested-With': 'XMLHttpRequest' };

/**
 * Sends a request to the API and returns the JSON of the answer, still unchecked (`unknown`): the caller reads it
 * with the readers of `decode.ts`. Addresses are relative on purpose, like the sign-in: the panel reaches the API
 * through its own address (decision 0014).
 */
export async function adminRequest(url: string, init: RequestInit): Promise<unknown> {
  const response = await fetch(url, init);
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
export function adminUpload(url: string, form: FormData): Promise<unknown> {
  return adminRequest(url, { method: 'POST', headers: CSRF_HEADERS, body: form });
}

/** A request that changes data: JSON body and the header that protects against cross-site requests. */
export function adminSend(method: string, url: string, body: unknown): Promise<unknown> {
  return adminRequest(url, {
    method,
    headers: { ...CSRF_HEADERS, 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}
