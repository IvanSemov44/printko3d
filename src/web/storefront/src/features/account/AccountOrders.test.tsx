import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { findAccessibilityViolations } from '../../testing/axe';
import { orderFixture } from '../../testing/orderFixture';
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
  emailConfirmed: true,
  hasPassword: true,
  linkedProviders: [],
};

function row(number: number, status: string, total: number) {
  return {
    number,
    status,
    currency: 'EUR',
    total,
    itemCount: 2,
    createdAt: '2026-11-13T10:00:00+00:00',
  };
}

function page(items: unknown[], overrides: Record<string, unknown> = {}) {
  return { items, page: 1, pageSize: 10, totalCount: items.length, totalPages: 1, ...overrides };
}

type AccountResponder = (url: string) => Response | Promise<Response>;

function stub(account: AccountResponder) {
  return stubNetwork({
    settings: () => Response.json(settingsFixture()),
    categories: () => Response.json(categoriesFixture()),
    account: (url) => (url.endsWith('/me') ? Response.json(CUSTOMER) : account(url)),
  });
}

describe('the history of orders', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('lists the orders with their number, status and sum, each with a link to its page', async () => {
    const fetchMock = stub(() =>
      Response.json(page([row(12345678, 'Paid', 3600), row(87654321, 'Shipped', 1250)])),
    );
    renderApp('/account/orders');

    const table = await screen.findByRole('table', { name: 'Моите поръчки' });
    const first = within(table).getByRole('link', { name: 'PR-12345678' });
    expect(first).toHaveAttribute('href', '/account/orders/12345678');
    expect(within(table).getByText('Платена')).toBeVisible();
    expect(within(table).getByText('Изпратена')).toBeVisible();
    expect(within(table).getByText(/36/)).toBeVisible();
    expect(
      fetchMock.mock.calls.some(([url]) => url === '/api/v1/account/orders?page=1&pageSize=10'),
    ).toBe(true);
  });

  it('asks for the page of the address, and shows the paging', async () => {
    const fetchMock = stub(() =>
      Response.json(
        page([row(12345678, 'Paid', 3600)], { page: 2, totalCount: 25, totalPages: 3 }),
      ),
    );
    renderApp('/account/orders?page=2');

    expect(await screen.findByRole('navigation', { name: 'Страниране' })).toBeVisible();
    expect(
      fetchMock.mock.calls.some(([url]) => url === '/api/v1/account/orders?page=2&pageSize=10'),
    ).toBe(true);
  });

  it('says that there are no orders yet', async () => {
    stub(() => Response.json(page([])));
    renderApp('/account/orders');

    expect(await screen.findByText('Още нямаш поръчки.')).toBeVisible();
  });

  it('sends a visitor to the sign-in', async () => {
    stub(() => Response.json({ code: 'auth.unauthenticated' }, { status: 401 }));
    renderApp('/account/orders');

    expect(await screen.findByRole('heading', { level: 1, name: 'Вход' })).toBeVisible();
  });

  it('is reached from the profile', async () => {
    stub(() => Response.json(page([])));
    const user = userEvent.setup();
    renderApp('/account');

    await user.click(await screen.findByRole('link', { name: 'Моите поръчки' }));

    expect(await screen.findByText('Още нямаш поръчки.')).toBeVisible();
  });

  it('shows one order with its steps and lines', async () => {
    stub(() => Response.json(orderFixture({ status: 'InProduction' })));
    renderApp('/account/orders/12345678');

    expect(await screen.findByText('PR-12345678')).toBeVisible();
    expect(screen.getByText('Dragon', { exact: false })).toBeVisible();
    expect(screen.getByRole('link', { name: 'Към всички поръчки' })).toHaveAttribute(
      'href',
      '/account/orders',
    );
  });

  it('says the same for an order that is missing and one that is another buyers', async () => {
    stub(() => Response.json({ code: 'order.not_found' }, { status: 404 }));
    renderApp('/account/orders/12345678');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Не намерихме такава поръчка в профила ти.',
    );
  });

  it('does not ask the server for something that is not an order number', async () => {
    const fetchMock = stub(() => Response.json(orderFixture()));
    renderApp('/account/orders/abc');

    expect(await screen.findByRole('alert')).toHaveTextContent('Не намерихме такава поръчка');
    expect(fetchMock.mock.calls.some(([url]) => url.startsWith('/api/v1/account/orders/'))).toBe(
      false,
    );
  });

  it('has no accessibility violations on the list and the order', async () => {
    stub((url) =>
      url.includes('/orders/')
        ? Response.json(orderFixture())
        : Response.json(page([row(12345678, 'Paid', 3600)])),
    );
    for (const path of ['/account/orders', '/account/orders/12345678']) {
      const { unmount } = renderApp(path);
      await findContent();
      await screen.findByRole('heading', { level: 1 });
      expect(await findAccessibilityViolations()).toEqual([]);
      unmount();
    }
  });
});
