import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { findAccessibilityViolations } from '../../testing/axe';
import {
  categoriesFixture,
  findContent,
  renderApp,
  settingsFixture,
  stubNetwork,
} from '../../testing/renderApp';

type AccountResponder = (url: string, init?: RequestInit) => Response | Promise<Response>;

function stub(account: AccountResponder) {
  return stubNetwork({
    settings: () => Response.json(settingsFixture()),
    categories: () => Response.json(categoriesFixture()),
    account,
  });
}

function bodyOf(init: RequestInit | undefined): unknown {
  return typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined;
}

const LINK = '?email=buyer%40example.test&token=a%2Fb%2Bc%3D';

describe('confirming the address', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends the address and the token of the link, once, and says it is done', async () => {
    const fetchMock = stub((url) =>
      url.endsWith('/confirm-email')
        ? new Response(null, { status: 204 })
        : Response.json({}, { status: 401 }),
    );
    renderApp(`/account/confirm-email${LINK}`);

    expect(await screen.findByText('Имейлът е потвърден.')).toBeVisible();
    const calls = fetchMock.mock.calls.filter(([url]) => url === '/api/v1/account/confirm-email');
    expect(calls).toHaveLength(1);
    expect(bodyOf(calls[0]?.[1])).toEqual({ email: 'buyer@example.test', token: 'a/b+c=' });
  });

  it('says that an old or wrong link does not work', async () => {
    stub((url) =>
      url.endsWith('/confirm-email')
        ? Response.json({ code: 'account.token_invalid' }, { status: 422 })
        : Response.json({}, { status: 401 }),
    );
    renderApp(`/account/confirm-email${LINK}`);

    expect(await screen.findByRole('alert')).toHaveTextContent('Връзката е стара или не е вярна.');
  });

  it('asks the server nothing when the link has no token', async () => {
    const fetchMock = stub(() => Response.json({}, { status: 401 }));
    renderApp('/account/confirm-email?email=buyer%40example.test');

    expect(await screen.findByRole('alert')).toHaveTextContent('Връзката е стара или не е вярна.');
    expect(fetchMock.mock.calls.some(([url]) => url === '/api/v1/account/confirm-email')).toBe(
      false,
    );
  });

  it('sends the confirmation email again from the profile', async () => {
    const fetchMock = stub((url, init) => {
      if (url.endsWith('/resend-confirmation')) {
        return new Response(null, { status: 202 });
      }
      return init?.method === undefined
        ? Response.json({
            email: 'buyer@example.test',
            displayName: 'Buyer',
            emailConfirmed: false,
            hasPassword: true,
            linkedProviders: [],
          })
        : new Response(null, { status: 204 });
    });
    const user = userEvent.setup();
    renderApp('/account');

    await user.click(await screen.findByRole('button', { name: 'Изпрати писмото пак' }));

    expect(await screen.findByText('Изпратихме ново писмо. Провери пощата си.')).toBeVisible();
    expect(
      fetchMock.mock.calls.some(([url]) => url === '/api/v1/account/resend-confirmation'),
    ).toBe(true);
  });
});

describe('forgotten password', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('is reached from the sign-in', async () => {
    stub(() => Response.json({}, { status: 401 }));
    const user = userEvent.setup();
    renderApp('/account/login');

    await user.click(await screen.findByRole('link', { name: 'Забравена парола' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Забравена парола' }),
    ).toBeVisible();
  });

  it('asks for the link and gives one answer, whatever the address', async () => {
    const fetchMock = stub((url) =>
      url.endsWith('/forgot-password')
        ? new Response(null, { status: 202 })
        : Response.json({}, { status: 401 }),
    );
    const user = userEvent.setup();
    renderApp('/account/forgot-password');

    await user.type(await screen.findByLabelText('Имейл'), 'anyone@example.test');
    await user.click(screen.getByRole('button', { name: 'Изпрати връзката' }));

    expect(await screen.findByRole('status')).toHaveTextContent('Ако има акаунт с този имейл');
    const call = fetchMock.mock.calls.find(([url]) => url === '/api/v1/account/forgot-password');
    expect(bodyOf(call?.[1])).toEqual({ email: 'anyone@example.test' });
  });

  it('does not ask for a link for something that is not an address', async () => {
    const fetchMock = stub(() => Response.json({}, { status: 401 }));
    const user = userEvent.setup();
    renderApp('/account/forgot-password');

    await user.type(await screen.findByLabelText('Имейл'), 'not an address');
    await user.click(screen.getByRole('button', { name: 'Изпрати връзката' }));

    expect(screen.getByLabelText('Имейл')).toHaveFocus();
    expect(fetchMock.mock.calls.some(([url]) => url === '/api/v1/account/forgot-password')).toBe(
      false,
    );
  });
});

describe('a new password from the link', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends the address, the token and the password, and leads to the sign-in', async () => {
    const fetchMock = stub((url) =>
      url.endsWith('/reset-password')
        ? new Response(null, { status: 204 })
        : Response.json({}, { status: 401 }),
    );
    const user = userEvent.setup();
    renderApp(`/account/reset-password${LINK}`);

    await user.type(await screen.findByLabelText('Нова парола'), 'a made-up password');
    await user.click(screen.getByRole('button', { name: 'Задай паролата' }));

    expect(await screen.findByText('Паролата е сменена. Влез с новата.')).toBeVisible();
    expect(within(await findContent()).getByRole('link', { name: 'Вход' })).toHaveAttribute(
      'href',
      '/account/login',
    );
    const call = fetchMock.mock.calls.find(([url]) => url === '/api/v1/account/reset-password');
    expect(bodyOf(call?.[1])).toEqual({
      email: 'buyer@example.test',
      token: 'a/b+c=',
      newPassword: 'a made-up password',
    });
  });

  it('refuses a short password before it asks the server', async () => {
    const fetchMock = stub(() => Response.json({}, { status: 401 }));
    const user = userEvent.setup();
    renderApp(`/account/reset-password${LINK}`);

    await user.type(await screen.findByLabelText('Нова парола'), 'short');
    await user.click(screen.getByRole('button', { name: 'Задай паролата' }));

    expect(screen.getByLabelText('Нова парола')).toHaveAccessibleDescription(
      'Поне 12 знака. Паролата е твърде кратка.',
    );
    expect(fetchMock.mock.calls.some(([url]) => url === '/api/v1/account/reset-password')).toBe(
      false,
    );
  });

  it('says that a used or old link does not work, and leads to a new request', async () => {
    stub((url) =>
      url.endsWith('/reset-password')
        ? Response.json({ code: 'account.token_invalid' }, { status: 422 })
        : Response.json({}, { status: 401 }),
    );
    const user = userEvent.setup();
    renderApp(`/account/reset-password${LINK}`);

    await user.type(await screen.findByLabelText('Нова парола'), 'a made-up password');
    await user.click(screen.getByRole('button', { name: 'Задай паролата' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Връзката е стара или вече е ползвана.',
    );
    await waitFor(() => {
      expect(screen.getByRole('link', { name: 'Забравена парола' })).toHaveAttribute(
        'href',
        '/account/forgot-password',
      );
    });
  });

  it('shows the way to a new request when the link has no token', async () => {
    stub(() => Response.json({}, { status: 401 }));
    renderApp('/account/reset-password');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Връзката е стара или вече е ползвана.',
    );
  });

  it('has no accessibility violations on the pages', async () => {
    stub(() => Response.json({}, { status: 401 }));
    for (const path of [
      '/account/forgot-password',
      `/account/reset-password${LINK}`,
      '/account/reset-password',
    ]) {
      const { unmount } = renderApp(path);
      await findContent();
      await screen.findByRole('heading', { level: 1 });
      expect(await findAccessibilityViolations()).toEqual([]);
      unmount();
    }
  });
});
