import { QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import { createQueryClient } from './queryClient';

interface Handlers {
  me?: () => Response | Promise<Response>;
  login?: (body: unknown) => Response;
  logout?: () => Response;
  healthOk?: boolean;
}

const ADMIN = { email: 'admin@example.test' };
// A made-up password for tests only.
const PASSWORD = 'a made-up long password';

/** Replaces fetch: the panel is tested against the address and the answers, not against its own code. */
function stubApi(handlers: Handlers) {
  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET';
    if (url === '/config.json') {
      return Promise.resolve(Response.json({ apiUrl: 'http://api.test' }));
    }
    if (url === 'http://api.test/health') {
      return Promise.resolve(
        new Response(null, { status: handlers.healthOk === false ? 503 : 200 }),
      );
    }
    if (url === '/api/v1/auth/me' && method === 'GET') {
      return Promise.resolve(handlers.me ? handlers.me() : new Response(null, { status: 401 }));
    }
    if (url === '/api/v1/auth/login' && method === 'POST') {
      const body: unknown = JSON.parse(bodyText(init));
      return Promise.resolve(
        handlers.login ? handlers.login(body) : new Response(null, { status: 401 }),
      );
    }
    if (url === '/api/v1/auth/logout' && method === 'POST') {
      return Promise.resolve(
        handlers.logout ? handlers.logout() : new Response(null, { status: 204 }),
      );
    }
    return Promise.reject(new Error(`Unexpected request: ${method} ${url}`));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function requestsTo(fetchMock: ReturnType<typeof stubApi>, url: string) {
  return fetchMock.mock.calls.filter(([called]) => called === url);
}

function renderApp() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <App />
    </QueryClientProvider>,
  );
}

async function fillAndSubmit(email: string, password: string) {
  fireEvent.change(await screen.findByLabelText('Имейл'), { target: { value: email } });
  fireEvent.change(screen.getByLabelText('Парола'), { target: { value: password } });
  fireEvent.click(screen.getByRole('button', { name: 'Влез' }));
}

/** The request body as text; the requests of these tests always carry a JSON string. */
function bodyText(init: RequestInit | undefined): string {
  return typeof init?.body === 'string' ? init.body : '';
}

const problem = (status: number, code: string) => Response.json({ code }, { status });

describe('Admin panel', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows only a loading message until the API says who is here', () => {
    stubApi({ me: () => new Promise<Response>(() => undefined) });
    renderApp();

    expect(screen.getByRole('status')).toHaveTextContent('Зареждане…');
    expect(screen.queryByLabelText('Парола')).not.toBeInTheDocument();
  });

  describe('a visitor who is not signed in', () => {
    it('sees only the sign-in form, and the panel does not even ask for the API status', async () => {
      const fetchMock = stubApi({});
      renderApp();

      expect(await screen.findByLabelText('Имейл')).toBeInTheDocument();
      expect(screen.getByLabelText('Парола')).toBeInTheDocument();
      expect(screen.queryByText('API е достъпно')).not.toBeInTheDocument();
      expect(requestsTo(fetchMock, 'http://api.test/health')).toHaveLength(0);
    });

    it('also gets the form when the account has no Admin role (403)', async () => {
      stubApi({ me: () => new Response(null, { status: 403 }) });
      renderApp();

      expect(await screen.findByLabelText('Имейл')).toBeInTheDocument();
    });

    it('signs in with the email and the password and then shows who is signed in', async () => {
      const fetchMock = stubApi({ login: () => Response.json(ADMIN) });
      renderApp();

      await fillAndSubmit(' admin@example.test ', PASSWORD);

      expect(await screen.findByText(ADMIN.email)).toBeInTheDocument();
      expect(screen.queryByLabelText('Парола')).not.toBeInTheDocument();
      expect(await screen.findByText('API е достъпно')).toBeInTheDocument();
      const [, init] = requestsTo(fetchMock, '/api/v1/auth/login')[0] ?? [];
      expect(JSON.parse(bodyText(init))).toEqual({ email: ADMIN.email, password: PASSWORD });
      expect(init?.headers).toMatchObject({ 'X-Requested-With': 'XMLHttpRequest' });
    });

    it('gets one general message for a failed sign-in and the password field is emptied', async () => {
      stubApi({ login: () => problem(401, 'auth.invalid_credentials') });
      renderApp();

      await fillAndSubmit('admin@example.test', 'wrong password');

      expect(await screen.findByRole('alert')).toHaveTextContent('Грешен имейл или парола.');
      expect(screen.getByLabelText('Парола')).toHaveValue('');
      expect(screen.getByLabelText('Парола')).toHaveFocus();
      expect(screen.getByLabelText('Имейл')).toHaveValue('admin@example.test');
    });

    it('is told to wait after too many attempts', async () => {
      stubApi({ login: () => problem(429, 'x') });
      renderApp();

      await fillAndSubmit('admin@example.test', PASSWORD);

      expect(await screen.findByRole('alert')).toHaveTextContent('Твърде много опити');
    });

    it('gets a neutral message when the API fails', async () => {
      stubApi({ login: () => problem(500, 'x') });
      renderApp();

      await fillAndSubmit('admin@example.test', PASSWORD);

      expect(await screen.findByRole('alert')).toHaveTextContent('Входът не е възможен в момента');
    });

    it('is not sent anywhere when the form is empty, and sees what is missing next to each field', async () => {
      const fetchMock = stubApi({});
      renderApp();
      await screen.findByLabelText('Имейл');

      fireEvent.click(screen.getByRole('button', { name: 'Влез' }));

      const email = screen.getByLabelText('Имейл');
      const password = screen.getByLabelText('Парола');
      expect(email).toHaveAccessibleDescription('Въведи имейл.');
      expect(email).toBeInvalid();
      expect(password).toHaveAccessibleDescription('Въведи парола.');
      expect(email).toHaveFocus();
      expect(requestsTo(fetchMock, '/api/v1/auth/login')).toHaveLength(0);
    });

    it('is told when the email does not look right', async () => {
      const fetchMock = stubApi({});
      renderApp();

      await fillAndSubmit('not-an-email', PASSWORD);

      expect(screen.getByLabelText('Имейл')).toHaveAccessibleDescription(/не изглежда правилно/);
      expect(requestsTo(fetchMock, '/api/v1/auth/login')).toHaveLength(0);
    });
  });

  describe('a signed-in administrator', () => {
    it('sees their email and the API status without a sign-in form', async () => {
      stubApi({ me: () => Response.json(ADMIN) });
      renderApp();

      expect(await screen.findByText(ADMIN.email)).toBeInTheDocument();
      expect(await screen.findByText('API е достъпно')).toBeInTheDocument();
      expect(screen.queryByLabelText('Парола')).not.toBeInTheDocument();
    });

    it('is signed out by the button and sees only the form again', async () => {
      const fetchMock = stubApi({ me: () => Response.json(ADMIN) });
      renderApp();
      fireEvent.click(await screen.findByRole('button', { name: 'Изход' }));

      expect(await screen.findByLabelText('Парола')).toBeInTheDocument();
      expect(screen.queryByText(ADMIN.email)).not.toBeInTheDocument();
      const [, init] = requestsTo(fetchMock, '/api/v1/auth/logout')[0] ?? [];
      expect(init?.method).toBe('POST');
      expect(init?.headers).toMatchObject({ 'X-Requested-With': 'XMLHttpRequest' });
    });

    it('stays signed in and is told so when the sign-out fails', async () => {
      stubApi({ me: () => Response.json(ADMIN), logout: () => problem(500, 'x') });
      renderApp();
      fireEvent.click(await screen.findByRole('button', { name: 'Изход' }));

      expect(await screen.findByRole('alert')).toHaveTextContent('Изходът не успя');
      expect(screen.getByText(ADMIN.email)).toBeInTheDocument();
    });

    it('is shown the form when the session turns out to be already gone while signing out (401)', async () => {
      stubApi({
        me: () => Response.json(ADMIN),
        logout: () => new Response(null, { status: 401 }),
      });
      renderApp();
      fireEvent.click(await screen.findByRole('button', { name: 'Изход' }));

      expect(await screen.findByLabelText('Парола')).toBeInTheDocument();
    });
  });

  it('does not take a failing API for "signed out": it says so and offers another try', async () => {
    let healthy = false;
    stubApi({ me: () => (healthy ? new Response(null, { status: 401 }) : problem(500, 'x')) });
    renderApp();

    expect(await screen.findByRole('alert')).toHaveTextContent('Не успяхме да проверим');
    expect(screen.queryByLabelText('Парола')).not.toBeInTheDocument();

    healthy = true;
    fireEvent.click(screen.getByRole('button', { name: 'Опитай отново' }));

    await waitFor(() => {
      expect(screen.getByLabelText('Парола')).toBeInTheDocument();
    });
  });
});
