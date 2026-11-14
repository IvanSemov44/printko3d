import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from './client';
import {
  changeCustomerPassword,
  getCurrentCustomer,
  loginCustomer,
  logoutCustomer,
  registerCustomer,
} from './account';
import { UnexpectedResponseError } from './decode';

const DTO = {
  email: 'buyer@example.test',
  displayName: 'Test Buyer',
  emailConfirmed: false,
  hasPassword: true,
};

function stubFetch(body: unknown, status = 200) {
  const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(() =>
    Promise.resolve(
      status === 204 || status === 202
        ? new Response(null, { status })
        : Response.json(body, { status }),
    ),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('the account of a buyer', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('signs in with the protective header and reads the buyer', async () => {
    const fetchMock = stubFetch(DTO);

    const customer = await loginCustomer('buyer@example.test', 'a made-up password');

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/account/login');
    expect(init?.method).toBe('POST');
    expect(new Headers(init?.headers).get('X-Requested-With')).toBe('XMLHttpRequest');
    expect(customer).toEqual(DTO);
  });

  it('turns a wrong password into an ApiError with status 401', async () => {
    stubFetch({ code: 'auth.invalid_credentials' }, 401);

    await expect(loginCustomer('buyer@example.test', 'wrong')).rejects.toMatchObject({
      status: 401,
      code: 'auth.invalid_credentials',
    });
  });

  it('reads no buyer, not an error, when there is no session', async () => {
    stubFetch({}, 401);

    await expect(getCurrentCustomer()).resolves.toBeNull();
  });

  it('does not hide another failure of the session check', async () => {
    stubFetch({}, 500);

    await expect(getCurrentCustomer()).rejects.toBeInstanceOf(ApiError);
  });

  it('names the field when the answer has the wrong shape', async () => {
    stubFetch({ ...DTO, hasPassword: 'yes' });

    await expect(getCurrentCustomer()).rejects.toThrow(UnexpectedResponseError);
    await expect(getCurrentCustomer()).rejects.toThrow(/customer\.hasPassword/);
  });

  it('registers with the fields and no sign-in answer', async () => {
    const fetchMock = stubFetch(null, 202);

    await registerCustomer({
      email: 'buyer@example.test',
      password: 'a made-up password',
      name: 'Buyer',
    });

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/account/register');
    expect(typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined).toEqual(
      {
        email: 'buyer@example.test',
        password: 'a made-up password',
        name: 'Buyer',
      },
    );
  });

  it('turns a refused registration into an ApiError with the rejected fields', async () => {
    stubFetch({ code: 'validation.failed', errors: { password: ['PasswordTooShort'] } }, 400);

    await expect(
      registerCustomer({ email: 'buyer@example.test', password: 'short', name: 'Buyer' }),
    ).rejects.toMatchObject({ status: 400, code: 'validation.failed', fields: ['password'] });
  });

  it('signs out and changes the password with the protective header', async () => {
    const fetchMock = stubFetch(null, 204);

    await logoutCustomer();
    await changeCustomerPassword({
      currentPassword: 'old one here',
      newPassword: 'new one here ok',
    });

    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      '/api/v1/account/logout',
      '/api/v1/account/change-password',
    ]);
    for (const [, init] of fetchMock.mock.calls) {
      expect(new Headers(init?.headers).get('X-Requested-With')).toBe('XMLHttpRequest');
    }
  });
});
