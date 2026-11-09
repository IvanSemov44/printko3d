import { ApiError, readErrorCode } from './client';
import type { components } from './schema';

export type AdminUser = components['schemas']['AdminUserDto'];

// The admin panel talks to the API through its own address (the nginx proxy, decision 0014), so the
// session cookie belongs to one site. That is why these paths are relative and do not use `apiUrl`.
const AUTH_PATH = '/api/v1/auth';

/** Every request that changes data must carry this header, or the API answers 403 (decision 0014). */
const CSRF_HEADERS = { 'X-Requested-With': 'XMLHttpRequest' };

function isAdminUser(value: unknown): value is AdminUser {
  return (
    typeof value === 'object' &&
    value !== null &&
    'email' in value &&
    typeof value.email === 'string'
  );
}

async function readAdminUser(response: Response): Promise<AdminUser> {
  const body: unknown = await response.json();
  if (!isAdminUser(body)) {
    throw new Error('The API answered with an invalid user');
  }
  return body;
}

/** The signed-in administrator, or null when there is no session (401) or the account has no Admin role (403). */
export async function getCurrentAdmin(signal?: AbortSignal): Promise<AdminUser | null> {
  const response = await fetch(`${AUTH_PATH}/me`, { signal });
  if (response.status === 401 || response.status === 403) {
    return null;
  }
  if (!response.ok) {
    throw new ApiError(response.status, await readErrorCode(response));
  }
  return readAdminUser(response);
}

/**
 * Signs in. A wrong email, a wrong password and a locked account are all an ApiError with status 401,
 * on purpose: the API does not say which one it was.
 */
export async function login(email: string, password: string): Promise<AdminUser> {
  const response = await fetch(`${AUTH_PATH}/login`, {
    method: 'POST',
    headers: { ...CSRF_HEADERS, 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
  if (!response.ok) {
    throw new ApiError(response.status, await readErrorCode(response));
  }
  return readAdminUser(response);
}

export async function logout(): Promise<void> {
  const response = await fetch(`${AUTH_PATH}/logout`, { method: 'POST', headers: CSRF_HEADERS });
  if (!response.ok) {
    throw new ApiError(response.status, await readErrorCode(response));
  }
}
