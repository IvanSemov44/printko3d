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

const navigateAway = vi.hoisted(() => vi.fn<(address: string) => void>());
vi.mock('./navigateAway', () => ({ navigateAway }));

const CUSTOMER = {
  email: 'buyer@example.test',
  displayName: 'Test Buyer',
  emailConfirmed: true,
  hasPassword: false,
  linkedProviders: ['Google'],
};

function stub(options: { providers: string[]; customer?: typeof CUSTOMER | null }) {
  return stubNetwork({
    settings: () => Response.json(settingsFixture()),
    categories: () => Response.json(categoriesFixture()),
    account: (url, init) => {
      if (url === '/api/v1/account/external/providers') {
        return Response.json({ providers: options.providers });
      }
      if (url.endsWith('/link')) {
        return new Response(null, { status: init?.method === 'POST' ? 204 : 405 });
      }
      return url.endsWith('/me') && options.customer
        ? Response.json(options.customer)
        : Response.json({ code: 'auth.unauthenticated' }, { status: 401 });
    },
  });
}

describe('sign-in through a social provider', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    navigateAway.mockClear();
  });

  it('shows a button for each provider that is turned on, as a link that leaves for the provider', async () => {
    stub({ providers: ['Google', 'Facebook'] });
    renderApp('/account/login');

    const google = await screen.findByRole('link', { name: 'Продължи с Google' });
    expect(google).toHaveAttribute('href', '/api/v1/account/external/Google/start');
    expect(screen.getByRole('link', { name: 'Продължи с Facebook' })).toHaveAttribute(
      'href',
      '/api/v1/account/external/Facebook/start',
    );
  });

  it('shows the buttons on the registration page too', async () => {
    stub({ providers: ['Google'] });
    renderApp('/account/register');

    expect(await screen.findByRole('link', { name: 'Продължи с Google' })).toBeVisible();
  });

  it('shows no button when no provider is turned on, and the form still works', async () => {
    stub({ providers: [] });
    renderApp('/account/login');

    expect(await screen.findByRole('button', { name: 'Влез' })).toBeVisible();
    expect(screen.queryByRole('link', { name: /Продължи с/ })).toBeNull();
  });

  it.each([
    ['external_failed', 'Входът през външния доставчик не стана. Опитай пак.'],
    ['account_exists', 'Вече има акаунт с този имейл.'],
    ['email_not_verified', 'Доставчикът не потвърди имейл адреса ти'],
    ['already_linked', 'вече е свързан с друг профил'],
  ])('says why the sign-in did not work: %s', async (code, text) => {
    stub({ providers: ['Google'] });
    renderApp(`/account/login?error=${code}`);

    expect(await screen.findByRole('alert')).toHaveTextContent(text);
  });

  it('shows nothing for a reason it does not know', async () => {
    stub({ providers: ['Google'] });
    renderApp('/account/login?error=made-up-reason');

    await screen.findByRole('button', { name: 'Влез' });
    expect(screen.queryByRole('alert')).toBeNull();
  });
});

describe('the connected providers in the profile', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    navigateAway.mockClear();
  });

  it('marks the provider that is connected and offers the other', async () => {
    stub({ providers: ['Google', 'Facebook'], customer: CUSTOMER });
    renderApp('/account');

    expect(await screen.findByText('Google: свързан')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Свържи Facebook' })).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Свържи Google' })).toBeNull();
  });

  it('asks the server first, and then leaves for the provider', async () => {
    const fetchMock = stub({ providers: ['Google', 'Facebook'], customer: CUSTOMER });
    const user = userEvent.setup();
    renderApp('/account');

    await user.click(await screen.findByRole('button', { name: 'Свържи Facebook' }));

    await waitFor(() => {
      expect(navigateAway).toHaveBeenCalledWith(
        '/api/v1/account/external/Facebook/start?mode=link',
      );
    });
    const call = fetchMock.mock.calls.find(
      ([url]) => url === '/api/v1/account/external/Facebook/link',
    );
    expect(call?.[1]?.method).toBe('POST');
    expect(new Headers(call?.[1]?.headers).get('X-Requested-With')).toBe('XMLHttpRequest');
  });

  it('does not leave when the server refuses, and says so', async () => {
    stubNetwork({
      settings: () => Response.json(settingsFixture()),
      categories: () => Response.json(categoriesFixture()),
      account: (url) => {
        if (url === '/api/v1/account/external/providers') {
          return Response.json({ providers: ['Facebook'] });
        }
        return url.endsWith('/me')
          ? Response.json({ ...CUSTOMER, linkedProviders: [] })
          : Response.json({}, { status: 404 });
      },
    });
    const user = userEvent.setup();
    renderApp('/account');

    await user.click(await screen.findByRole('button', { name: 'Свържи Facebook' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Не се получи');
    expect(navigateAway).not.toHaveBeenCalled();
  });

  it('says that a provider was connected', async () => {
    stub({ providers: ['Google'], customer: CUSTOMER });
    renderApp('/account?linked=Google');

    expect(await screen.findByText('Google е свързан с профила ти.')).toBeVisible();
  });

  it('says why a provider was not connected', async () => {
    stub({ providers: ['Google'], customer: CUSTOMER });
    renderApp('/account?error=already_linked');

    expect(await screen.findByRole('alert')).toHaveTextContent('вече е свързан с друг профил');
  });

  it('shows no section when no provider is turned on', async () => {
    stub({ providers: [], customer: CUSTOMER });
    renderApp('/account');

    expect(await screen.findByRole('heading', { level: 1, name: 'Моят профил' })).toBeVisible();
    expect(screen.queryByRole('heading', { name: 'Свързани входове' })).toBeNull();
  });

  it('has no accessibility violations with the buttons', async () => {
    stub({ providers: ['Google', 'Facebook'], customer: CUSTOMER });
    for (const path of ['/account/login', '/account/register', '/account']) {
      const { unmount } = renderApp(path);
      await findContent();
      await screen.findByRole('heading', { level: 1 });
      await screen.findAllByText(/Google/);
      expect(await findAccessibilityViolations()).toEqual([]);
      unmount();
    }
  });
});
