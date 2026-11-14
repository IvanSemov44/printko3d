import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from './client';
import {
  changeCustomerPassword,
  confirmCustomerEmail,
  externalLinkAddress,
  externalSignInAddress,
  getExternalProviders,
  prepareExternalLink,
  requestPasswordReset,
  resendCustomerConfirmation,
  resetCustomerPassword,
  getCurrentCustomer,
  getMyOrder,
  getMyOrders,
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
  linkedProviders: [],
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

describe('the emails of an account', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends the address and the token of the link in a body, with the protective header', async () => {
    const fetchMock = stubFetch(null, 204);

    await confirmCustomerEmail('buyer@example.test', 'a/token+with=marks');

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/account/confirm-email');
    expect(new Headers(init?.headers).get('X-Requested-With')).toBe('XMLHttpRequest');
    expect(typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined).toEqual(
      {
        email: 'buyer@example.test',
        token: 'a/token+with=marks',
      },
    );
  });

  it('turns an old or wrong link into an ApiError with the code', async () => {
    stubFetch({ code: 'account.token_invalid' }, 422);

    await expect(confirmCustomerEmail('buyer@example.test', 'old')).rejects.toMatchObject({
      status: 422,
      code: 'account.token_invalid',
    });
  });

  it('asks for the link, resends the confirmation and sets the new password', async () => {
    const fetchMock = stubFetch(null, 204);

    await requestPasswordReset('buyer@example.test');
    await resendCustomerConfirmation();
    await resetCustomerPassword({
      email: 'buyer@example.test',
      token: 'token',
      newPassword: 'a made-up password',
    });

    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      '/api/v1/account/forgot-password',
      '/api/v1/account/resend-confirmation',
      '/api/v1/account/reset-password',
    ]);
  });
});

describe('the orders of a buyer', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  const PAGE = {
    items: [
      {
        number: 12345678,
        status: 'Paid',
        currency: 'BGN',
        total: '3600',
        itemCount: 2,
        createdAt: '2026-11-13T10:00:00+00:00',
      },
    ],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1,
  };

  it('asks for a page and reads the rows, with the money as numbers', async () => {
    const fetchMock = stubFetch(PAGE);

    const page = await getMyOrders({ page: 2, pageSize: 5 });

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/account/orders?page=2&pageSize=5');
    expect(page.items).toEqual([
      {
        number: 12345678,
        status: 'Paid',
        currency: 'BGN',
        total: 3600,
        itemCount: 2,
        createdAt: '2026-11-13T10:00:00+00:00',
      },
    ]);
    expect(page.totalPages).toBe(1);
  });

  it('works out the pages when the server leaves them out', async () => {
    stubFetch({ ...PAGE, totalPages: undefined, totalCount: 41, pageSize: 20 });

    expect((await getMyOrders({ page: 1, pageSize: 20 })).totalPages).toBe(3);
  });

  it('refuses a row with an unknown status', async () => {
    stubFetch({ ...PAGE, items: [{ ...PAGE.items[0], status: 'Lost' }] });

    await expect(getMyOrders({ page: 1, pageSize: 20 })).rejects.toThrow(/status/);
  });

  it('reads one order by its number, and turns a refusal into an ApiError', async () => {
    const fetchMock = stubFetch({ code: 'order.not_found' }, 404);

    await expect(getMyOrder(12345678)).rejects.toMatchObject({
      status: 404,
      code: 'order.not_found',
    });
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/account/orders/12345678');
  });
});

describe('the social providers', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('reads the providers that are turned on', async () => {
    const fetchMock = stubFetch({ providers: ['Google', 'Facebook'] });

    expect(await getExternalProviders()).toEqual(['Google', 'Facebook']);
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/account/external/providers');
  });

  it('names the field when the answer has the wrong shape', async () => {
    stubFetch({ providers: 'Google' });

    await expect(getExternalProviders()).rejects.toThrow(/externalProviders\.providers/);
  });

  it('builds the addresses that the browser opens, with the provider escaped', () => {
    expect(externalSignInAddress('Google')).toBe('/api/v1/account/external/Google/start');
    expect(externalLinkAddress('Facebook')).toBe(
      '/api/v1/account/external/Facebook/start?mode=link',
    );
    expect(externalSignInAddress('a/b')).toBe('/api/v1/account/external/a%2Fb/start');
  });

  it('asks to connect a provider with the protective header', async () => {
    const fetchMock = stubFetch(null, 204);

    await prepareExternalLink('Google');

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/account/external/Google/link');
    expect(init?.method).toBe('POST');
    expect(new Headers(init?.headers).get('X-Requested-With')).toBe('XMLHttpRequest');
  });
});
