import type { AdminOrder, Order, OrderStatus } from '@storefront/shared';
import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../../queryClient';
import { OrderDetailPage } from './OrderDetailPage';
import { OrderListPage } from './OrderListPage';

const NUMBER = 12345678;

function order(status: OrderStatus, number = NUMBER): Order {
  return {
    number,
    status,
    currency: 'EUR',
    total: 4500,
    shippingMethod: 'address',
    shippingAmount: 600,
    customerName: 'Anna Test',
    email: 'anna@example.test',
    phone: '+359000000',
    shippingStreet: '1 Test Street',
    shippingCity: 'Sofia',
    shippingPostalCode: '1000',
    shippingCountry: 'BG',
    createdAt: '2026-11-14T10:00:00Z',
    paymentDueAt: null,
    items: [
      {
        productName: 'Dragon',
        variantName: 'Red',
        sku: 'DRG-RED',
        unitPrice: 1950,
        quantity: 2,
        lineTotal: 3900,
        fulfilmentType: 'InStock',
        leadTimeDays: null,
      },
    ],
  };
}

const NEXT: Record<string, OrderStatus[]> = {
  Paid: ['InProduction'],
  InProduction: ['ReadyToShip'],
  ReadyToShip: ['Shipped'],
  Shipped: ['Completed'],
};

function adminOrder(status: OrderStatus): AdminOrder {
  return {
    order: order(status),
    nextStatuses: NEXT[status] ?? [],
    canCancel: ['New', 'Paid', 'InProduction', 'ReadyToShip'].includes(status),
  };
}

function bodyOf(init: RequestInit | undefined): unknown {
  return typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined;
}

interface Api {
  status: OrderStatus;
  listUrls: string[];
  posts: { url: string; body: unknown }[];
}

/** A small API in memory: it keeps the status of one order and moves it like the server does. */
function fakeApi(
  options: { status?: OrderStatus; stepStatus?: number; totalPages?: number } = {},
): Api {
  const api: Api = { status: options.status ?? 'Paid', listUrls: [], posts: [] };
  const base = `/api/v1/admin/orders/${String(NUMBER)}`;
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string, init?: RequestInit) => {
      if (url === '/api/v1/store-settings') {
        return Promise.resolve(Response.json({ defaultCurrency: 'EUR', defaultLocale: 'bg' }));
      }
      if (url === '/api/v1/admin/orders/status-counts') {
        return Promise.resolve(
          Response.json([
            { status: 'New', count: 2 },
            { status: 'Paid', count: 3 },
            { status: 'InProduction', count: 1 },
          ]),
        );
      }
      if (url.startsWith('/api/v1/admin/orders?')) {
        api.listUrls.push(url);
        return Promise.resolve(
          Response.json({
            items: [
              {
                number: NUMBER,
                status: api.status,
                currency: 'EUR',
                total: 4500,
                customerName: 'Anna Test',
                email: 'anna@example.test',
                itemCount: 1,
                createdAt: '2026-11-14T10:00:00Z',
              },
            ],
            page: 1,
            pageSize: 20,
            totalCount: (options.totalPages ?? 1) * 20,
            totalPages: options.totalPages ?? 1,
          }),
        );
      }
      if (url === base && (init?.method ?? 'GET') === 'GET') {
        return Promise.resolve(Response.json(adminOrder(api.status)));
      }
      if (url === `${base}/status`) {
        api.posts.push({ url, body: bodyOf(init) });
        if (options.stepStatus) {
          return Promise.resolve(
            Response.json(
              { code: 'order.status_transition_invalid' },
              { status: options.stepStatus },
            ),
          );
        }
        api.status = (bodyOf(init) as { status: OrderStatus }).status;
        return Promise.resolve(Response.json(adminOrder(api.status)));
      }
      if (url === `${base}/cancel`) {
        api.posts.push({ url, body: bodyOf(init) });
        const wasPaid = api.status !== 'New';
        api.status = 'Cancelled';
        return Promise.resolve(
          Response.json({ order: adminOrder('Cancelled'), refundByHand: wasPaid }),
        );
      }
      return Promise.resolve(Response.json({ code: 'order.not_found' }, { status: 404 }));
    }),
  );
  return api;
}

function renderAt(path: string) {
  render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="orders" element={<OrderListPage />} />
          <Route path="orders/:number" element={<OrderDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('the list of orders', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the number, the buyer, the status and the sum of each order, with a link to it', async () => {
    fakeApi({ status: 'InProduction' });
    renderAt('/orders');

    const row = await screen.findByRole('row', { name: /PR-12345678/ });

    expect(within(row).getByRole('link', { name: 'PR-12345678' }).getAttribute('href')).toBe(
      '/orders/12345678',
    );
    expect(within(row).getByText('Anna Test')).toBeTruthy();
    expect(within(row).getByText('anna@example.test')).toBeTruthy();
    expect(within(row).getByText('В производство')).toBeTruthy();
    expect(within(row).getByText(/45,00/)).toBeTruthy();
  });

  it('asks the server for the status that is picked, and for the search that is typed', async () => {
    const api = fakeApi();
    const user = userEvent.setup();
    renderAt('/orders');
    await screen.findByRole('row', { name: /PR-12345678/ });

    await user.click(screen.getByRole('button', { name: /Платена/ }));
    await waitFor(() => {
      expect(api.listUrls.at(-1)).toContain('status=Paid');
    });
    await user.type(screen.getByLabelText(/Търсене/), 'anna');
    await user.click(screen.getByRole('button', { name: 'Търси' }));

    await waitFor(() => {
      expect(api.listUrls.at(-1)).toContain('search=anna');
    });
    expect(api.listUrls.at(-1)).toContain('status=Paid');
  });

  it('shows the number of orders next to each status and marks the chosen one', async () => {
    fakeApi();
    const user = userEvent.setup();
    renderAt('/orders');

    const paid = await screen.findByRole('button', { name: 'Платена 3' });
    expect(screen.getByRole('button', { name: 'Нова 2' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Всички статуси 6' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    expect(screen.getByRole('button', { name: 'Отказана 0' })).toBeTruthy();

    await user.click(paid);

    expect(screen.getByRole('button', { name: 'Платена 3' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    expect(screen.getByRole('button', { name: 'Всички статуси 6' })).toHaveAttribute(
      'aria-pressed',
      'false',
    );
  });

  it('goes to the next page when there is one', async () => {
    const api = fakeApi({ totalPages: 2 });
    const user = userEvent.setup();
    renderAt('/orders');
    await screen.findByRole('row', { name: /PR-12345678/ });

    await user.click(screen.getByRole('button', { name: 'Следваща страница' }));

    await waitFor(() => {
      expect(api.listUrls.at(-1)).toContain('page=2');
    });
  });

  it('says so when the list cannot be loaded, and loads it again on request', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(Response.json({}, { status: 500 }))),
    );
    renderAt('/orders');

    expect((await screen.findByRole('alert')).textContent).toContain('не можаха да се заредят');
    expect(screen.getByRole('button', { name: 'Опитай отново' })).toBeTruthy();
  });
});

describe('one order', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the lines, the contact, the address and the sum', async () => {
    fakeApi({ status: 'Paid' });
    renderAt(`/orders/${String(NUMBER)}`);

    expect(await screen.findByRole('heading', { name: 'Поръчка PR-12345678' })).toBeTruthy();
    expect(screen.getByText('Dragon')).toBeTruthy();
    expect(screen.getByText('DRG-RED')).toBeTruthy();
    expect(screen.getByText('Anna Test')).toBeTruthy();
    expect(screen.getByText(/1 Test Street, 1000 Sofia/)).toBeTruthy();
    expect(screen.getAllByText(/45,00/).length).toBeGreaterThan(0);
  });

  it('shows only the step that follows, and takes it', async () => {
    const api = fakeApi({ status: 'Paid' });
    const user = userEvent.setup();
    renderAt(`/orders/${String(NUMBER)}`);

    await user.click(await screen.findByRole('button', { name: 'Започни производство' }));

    expect(await screen.findByRole('button', { name: 'Готова за изпращане' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Започни производство' })).toBeNull();
    expect(api.posts).toEqual([
      { url: `/api/v1/admin/orders/${String(NUMBER)}/status`, body: { status: 'InProduction' } },
    ]);
  });

  it('tells that the buyer gets an email before the order is marked as shipped', async () => {
    fakeApi({ status: 'ReadyToShip' });
    renderAt(`/orders/${String(NUMBER)}`);

    expect(await screen.findByRole('button', { name: 'Отбележи като изпратена' })).toBeTruthy();
    expect(screen.getByText(/получава писмо/)).toBeTruthy();
  });

  it('has no step and no refusal for a finished order', async () => {
    fakeApi({ status: 'Completed' });
    renderAt(`/orders/${String(NUMBER)}`);

    expect(await screen.findByText('Няма следващи стъпки за тази поръчка.')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Откажи поръчката' })).toBeNull();
  });

  it('says the order changed, and loads it again, when the server refuses the step with 409', async () => {
    fakeApi({ status: 'Paid', stepStatus: 409 });
    const user = userEvent.setup();
    renderAt(`/orders/${String(NUMBER)}`);

    await user.click(await screen.findByRole('button', { name: 'Започни производство' }));

    expect((await screen.findByRole('alert')).textContent).toContain('променена междувременно');
  });

  it('refuses a new order after the dialog: nothing to refund', async () => {
    const api = fakeApi({ status: 'New' });
    const user = userEvent.setup();
    renderAt(`/orders/${String(NUMBER)}`);

    await user.click(await screen.findByRole('button', { name: 'Откажи поръчката' }));
    const dialog = await screen.findByRole('dialog', { name: /Да откажем ли поръчка PR-12345678/ });
    expect(within(dialog).queryByText(/върни му ги на ръка/)).toBeNull();
    await user.click(within(dialog).getByRole('button', { name: 'Откажи поръчката' }));

    expect(await screen.findByText(/Поръчката е отказана/)).toBeTruthy();
    expect(screen.queryByText(/върни му парите на ръка/)).toBeNull();
    expect(api.posts.map((post) => post.url)).toEqual([
      `/api/v1/admin/orders/${String(NUMBER)}/cancel`,
    ]);
  });

  it('warns about the refund before refusing a paid order, and reminds after', async () => {
    fakeApi({ status: 'InProduction' });
    const user = userEvent.setup();
    renderAt(`/orders/${String(NUMBER)}`);

    await user.click(await screen.findByRole('button', { name: 'Откажи поръчката' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/Купувачът е платил.*върни му ги на ръка/)).toBeTruthy();
    await user.click(within(dialog).getByRole('button', { name: 'Откажи поръчката' }));

    expect(await screen.findByText('Купувачът е платил: върни му парите на ръка.')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Откажи поръчката' })).toBeNull();
  });

  it('does not refuse when the dialog is closed with "Назад"', async () => {
    const api = fakeApi({ status: 'Paid' });
    const user = userEvent.setup();
    renderAt(`/orders/${String(NUMBER)}`);

    await user.click(await screen.findByRole('button', { name: 'Откажи поръчката' }));
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Назад' }),
    );

    expect(api.posts).toEqual([]);
  });

  it('says so for an order that does not exist', async () => {
    fakeApi();
    renderAt('/orders/99999999');

    expect(await screen.findByText('Поръчката не е намерена.')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Към поръчките' })).toBeTruthy();
  });
});
