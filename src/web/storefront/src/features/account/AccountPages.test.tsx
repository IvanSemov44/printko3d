import { screen, waitFor } from '@testing-library/react';
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

const CUSTOMER = {
  email: 'buyer@example.test',
  displayName: 'Test Buyer',
  emailConfirmed: false,
  hasPassword: true,
  linkedProviders: [],
};

type AccountResponder = (url: string, init?: RequestInit) => Response | Promise<Response>;

function stub(account?: AccountResponder) {
  return stubNetwork({
    settings: () => Response.json(settingsFixture()),
    categories: () => Response.json(categoriesFixture()),
    ...(account === undefined ? {} : { account }),
  });
}

function bodyOf(init: RequestInit | undefined): unknown {
  return typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined;
}

function location(): string {
  return screen.getByTestId('location').textContent;
}

describe('the account of a buyer', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows "Вход" in the header to a visitor and "Профил" to a signed-in buyer', async () => {
    stub();
    const { unmount } = renderApp('/');
    expect(await screen.findByRole('link', { name: 'Вход' })).toHaveAttribute(
      'href',
      '/account/login',
    );
    unmount();

    stub(() => Response.json(CUSTOMER));
    renderApp('/');
    expect(await screen.findByRole('link', { name: 'Профил' })).toHaveAttribute('href', '/account');
  });

  it('signs in with the email and the password, and opens the profile', async () => {
    const fetchMock = stub((url) =>
      url.endsWith('/login') ? Response.json(CUSTOMER) : Response.json({}, { status: 401 }),
    );
    const user = userEvent.setup();
    renderApp('/account/login');

    await user.type(await screen.findByLabelText('Имейл'), 'buyer@example.test');
    await user.type(screen.getByLabelText('Парола'), 'a made-up password');
    await user.click(screen.getByRole('button', { name: 'Влез' }));

    await waitFor(() => {
      expect(location()).toContain('/account PUSH');
    });
    const call = fetchMock.mock.calls.find(([url]) => url === '/api/v1/account/login');
    expect(bodyOf(call?.[1])).toEqual({
      email: 'buyer@example.test',
      password: 'a made-up password',
    });
    expect(new Headers(call?.[1]?.headers).get('X-Requested-With')).toBe('XMLHttpRequest');
  });

  it('says one thing for a wrong email and a wrong password', async () => {
    stub(() => Response.json({ code: 'auth.invalid_credentials' }, { status: 401 }));
    const user = userEvent.setup();
    renderApp('/account/login');

    await user.type(await screen.findByLabelText('Имейл'), 'buyer@example.test');
    await user.type(screen.getByLabelText('Парола'), 'not the password');
    await user.click(screen.getByRole('button', { name: 'Влез' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Грешен имейл или парола.');
    expect(location()).toContain('/account/login');
  });

  it('does not send an empty form, and focuses the first field with a problem', async () => {
    const fetchMock = stub();
    const user = userEvent.setup();
    renderApp('/account/login');

    await user.click(await screen.findByRole('button', { name: 'Влез' }));

    expect(screen.getByLabelText('Имейл')).toHaveFocus();
    expect(screen.getByLabelText('Имейл')).toHaveAccessibleDescription('Въведи имейл адрес.');
    expect(fetchMock.mock.calls.some(([url]) => url === '/api/v1/account/login')).toBe(false);
  });

  it('registers and signs the new buyer in', async () => {
    const fetchMock = stub((url) => {
      if (url.endsWith('/register')) {
        return new Response(null, { status: 202 });
      }
      return url.endsWith('/login') ? Response.json(CUSTOMER) : Response.json({}, { status: 401 });
    });
    const user = userEvent.setup();
    renderApp('/account/register');

    await user.type(await screen.findByLabelText('Име'), 'Test Buyer');
    await user.type(screen.getByLabelText('Имейл'), 'buyer@example.test');
    await user.type(screen.getByLabelText('Парола'), 'a made-up password');
    await user.click(screen.getByRole('button', { name: 'Регистрирай се' }));

    await waitFor(() => {
      expect(location()).toContain('/account PUSH');
    });
    const call = fetchMock.mock.calls.find(([url]) => url === '/api/v1/account/register');
    expect(bodyOf(call?.[1])).toEqual({
      email: 'buyer@example.test',
      password: 'a made-up password',
      name: 'Test Buyer',
    });
  });

  it('gives the same answer for an address that has an account, when the password does not open it', async () => {
    stub((url) =>
      url.endsWith('/register')
        ? new Response(null, { status: 202 })
        : Response.json({ code: 'auth.invalid_credentials' }, { status: 401 }),
    );
    const user = userEvent.setup();
    renderApp('/account/register');

    await user.type(await screen.findByLabelText('Име'), 'Test Buyer');
    await user.type(screen.getByLabelText('Имейл'), 'buyer@example.test');
    await user.type(screen.getByLabelText('Парола'), 'a made-up password');
    await user.click(screen.getByRole('button', { name: 'Регистрирай се' }));

    expect(await screen.findByRole('status')).toHaveTextContent('Заявката е приета');
  });

  it('refuses a short password before it asks the server', async () => {
    const fetchMock = stub();
    const user = userEvent.setup();
    renderApp('/account/register');

    await user.type(await screen.findByLabelText('Име'), 'Test Buyer');
    await user.type(screen.getByLabelText('Имейл'), 'buyer@example.test');
    await user.type(screen.getByLabelText('Парола'), 'short');
    await user.click(screen.getByRole('button', { name: 'Регистрирай се' }));

    expect(screen.getByLabelText('Парола')).toHaveAccessibleDescription(
      'Поне 12 знака. Паролата е твърде кратка.',
    );
    expect(fetchMock.mock.calls.some(([url]) => url === '/api/v1/account/register')).toBe(false);
  });

  it('sends a visitor from the profile to the sign-in', async () => {
    stub();
    renderApp('/account');

    await waitFor(() => {
      expect(location()).toContain('/account/login');
    });
  });

  it('shows the profile, and changes the password', async () => {
    const fetchMock = stub((url) =>
      url.endsWith('/change-password')
        ? new Response(null, { status: 204 })
        : Response.json(CUSTOMER),
    );
    const user = userEvent.setup();
    renderApp('/account');

    expect(await screen.findByRole('heading', { level: 1, name: 'Моят профил' })).toBeVisible();
    expect(screen.getByText('Test Buyer')).toBeVisible();
    expect(screen.getByText('Имейлът ти още не е потвърден.')).toBeVisible();
    await user.type(screen.getByLabelText('Сегашна парола'), 'a made-up password');
    await user.type(screen.getByLabelText('Нова парола'), 'another made-up password');
    await user.click(screen.getByRole('button', { name: 'Смени паролата' }));

    expect(await screen.findByText('Паролата е сменена.')).toBeVisible();
    const call = fetchMock.mock.calls.find(([url]) => url === '/api/v1/account/change-password');
    expect(bodyOf(call?.[1])).toEqual({
      currentPassword: 'a made-up password',
      newPassword: 'another made-up password',
    });
  });

  it('says that the current password is wrong', async () => {
    stub((url) =>
      url.endsWith('/change-password')
        ? Response.json({ code: 'account.current_password_wrong' }, { status: 422 })
        : Response.json(CUSTOMER),
    );
    const user = userEvent.setup();
    renderApp('/account');

    await user.type(await screen.findByLabelText('Сегашна парола'), 'wrong wrong wrong');
    await user.type(screen.getByLabelText('Нова парола'), 'another made-up password');
    await user.click(screen.getByRole('button', { name: 'Смени паролата' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Сегашната парола не е вярна.');
  });

  it('signs out and goes to the home page', async () => {
    const fetchMock = stub((url, init) =>
      init?.method === 'POST' && url.endsWith('/logout')
        ? new Response(null, { status: 204 })
        : Response.json(CUSTOMER),
    );
    const user = userEvent.setup();
    renderApp('/account');

    await user.click(await screen.findByRole('button', { name: 'Изход' }));

    await waitFor(() => {
      expect(location()).toMatch(/^\/ PUSH/);
    });
    expect(fetchMock.mock.calls.some(([url]) => url === '/api/v1/account/logout')).toBe(true);
  });

  it('has no accessibility violations on the three pages', async () => {
    stub(() => Response.json(CUSTOMER));
    for (const path of ['/account', '/account/login', '/account/register']) {
      const { unmount } = renderApp(path);
      await findContent();
      await screen.findByRole('heading', { level: 1 });
      expect(await findAccessibilityViolations()).toEqual([]);
      unmount();
    }
  });
});
