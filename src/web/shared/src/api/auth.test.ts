import { afterEach, describe, expect, it, vi } from 'vitest';
import { getCurrentAdmin, login, logout } from './auth';
import { ApiError } from './client';

function stubFetch(response: Response) {
  const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(() =>
    Promise.resolve(response),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

/** The request body as text; the requests of these tests always carry a JSON string. */
function bodyText(init: RequestInit | undefined): string {
  return typeof init?.body === 'string' ? init.body : '';
}

describe('admin auth API', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe('getCurrentAdmin', () => {
    it('returns the user of the session', async () => {
      const fetchMock = stubFetch(Response.json({ email: 'admin@example.test' }));

      const user = await getCurrentAdmin();

      expect(user).toEqual({ email: 'admin@example.test' });
      expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/auth/me');
    });

    it.each([401, 403])('returns null for %i, which only means "not signed in"', async (status) => {
      stubFetch(new Response(null, { status }));

      expect(await getCurrentAdmin()).toBeNull();
    });

    it('throws an ApiError for a server failure, so it is not mistaken for "signed out"', async () => {
      stubFetch(Response.json({ code: 'oops' }, { status: 500 }));

      await expect(getCurrentAdmin()).rejects.toMatchObject({ status: 500, code: 'oops' });
    });

    it('rejects an answer that is not a user', async () => {
      stubFetch(Response.json({ name: 'nobody' }));

      await expect(getCurrentAdmin()).rejects.toThrow('invalid user');
    });
  });

  describe('login', () => {
    it('posts the credentials as JSON with the CSRF header and returns the user', async () => {
      const fetchMock = stubFetch(Response.json({ email: 'admin@example.test' }));

      const user = await login('admin@example.test', 'a made-up password');

      expect(user.email).toBe('admin@example.test');
      const [url, init] = fetchMock.mock.calls[0] ?? [];
      expect(url).toBe('/api/v1/auth/login');
      expect(init?.method).toBe('POST');
      expect(init?.headers).toMatchObject({ 'X-Requested-With': 'XMLHttpRequest' });
      expect(JSON.parse(bodyText(init))).toEqual({
        email: 'admin@example.test',
        password: 'a made-up password',
      });
    });

    it('throws an ApiError with the status and the code of a failed sign-in', async () => {
      stubFetch(Response.json({ code: 'auth.invalid_credentials' }, { status: 401 }));

      const error: unknown = await login('a@example.test', 'x').catch((caught: unknown) => caught);

      expect(error).toBeInstanceOf(ApiError);
      expect(error).toMatchObject({ status: 401, code: 'auth.invalid_credentials' });
    });
  });

  describe('logout', () => {
    it('posts to the logout address with the CSRF header', async () => {
      const fetchMock = stubFetch(new Response(null, { status: 204 }));

      await logout();

      const [url, init] = fetchMock.mock.calls[0] ?? [];
      expect(url).toBe('/api/v1/auth/logout');
      expect(init?.method).toBe('POST');
      expect(init?.headers).toMatchObject({ 'X-Requested-With': 'XMLHttpRequest' });
    });

    it('throws when the API refuses', async () => {
      stubFetch(new Response(null, { status: 401 }));

      await expect(logout()).rejects.toMatchObject({ status: 401 });
    });
  });
});
