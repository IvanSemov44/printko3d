import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../../queryClient';
import { ShippingTariffPage } from './ShippingTariffPage';

const TARIFF_URL = '/api/v1/admin/shipping-tariff';

interface Step {
  maxWeightGrams: number;
  price: number;
}

/** A small API in memory: it keeps the steps, sorts them on a save like the server does, and can refuse. */
function fakeApi(options: { steps?: Step[]; reject?: Response } = {}) {
  const server = {
    steps: options.steps ?? [
      { maxWeightGrams: 1000, price: 600 },
      { maxWeightGrams: 5000, price: 900 },
    ],
    puts: [] as { steps: Step[] }[],
  };
  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET';
    if (url === '/api/v1/store-settings') {
      return Promise.resolve(Response.json({ defaultCurrency: 'EUR', defaultLocale: 'bg' }));
    }
    if (url !== TARIFF_URL) {
      return Promise.resolve(Response.json({}, { status: 404 }));
    }
    if (method === 'PUT') {
      const body = JSON.parse(typeof init?.body === 'string' ? init.body : '{}') as {
        steps: Step[];
      };
      server.puts.push(body);
      if (options.reject) {
        return Promise.resolve(options.reject.clone());
      }
      server.steps = [...body.steps].sort((a, b) => a.maxWeightGrams - b.maxWeightGrams);
    }
    return Promise.resolve(Response.json({ steps: server.steps }));
  });
  vi.stubGlobal('fetch', fetchMock);
  return server;
}

function renderPage() {
  render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter>
        <ShippingTariffPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

async function loaded() {
  await screen.findByRole('button', { name: 'Запази тарифата' });
}

const save = (user: ReturnType<typeof userEvent.setup>) =>
  user.click(screen.getByRole('button', { name: 'Запази тарифата' }));

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('delivery tariff screen', () => {
  it('shows the steps with the weight in grams and the price in the main unit of the currency', async () => {
    fakeApi();
    renderPage();
    await loaded();

    const first = screen.getByRole('group', { name: 'Стъпка 1' });
    expect(within(first).getByLabelText('Тегло до (грама)')).toHaveValue('1000');
    expect(within(first).getByLabelText('Цена (EUR)')).toHaveValue('6.00');
    const second = screen.getByRole('group', { name: 'Стъпка 2' });
    expect(within(second).getByLabelText('Цена (EUR)')).toHaveValue('9.00');
  });

  it('says so when the tariff cannot be loaded, and offers to try again', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(Response.json({}, { status: 500 }))),
    );
    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Тарифата не можа да се зареди');
    expect(screen.getByRole('button', { name: 'Опитай пак' })).toBeInTheDocument();
  });

  it('sends the steps with prices in the smallest unit, shows what the server answered, and says it saved', async () => {
    const server = fakeApi();
    const user = userEvent.setup();
    renderPage();
    await loaded();

    await user.click(screen.getByRole('button', { name: 'Добави стъпка' }));
    const added = screen.getByRole('group', { name: 'Стъпка 3' });
    await user.type(within(added).getByLabelText('Тегло до (грама)'), '500');
    await user.type(within(added).getByLabelText('Цена (EUR)'), '4,50');
    await save(user);

    await waitFor(() => {
      expect(screen.getByRole('status')).toHaveTextContent('Тарифата е запазена.');
    });
    expect(server.puts).toEqual([
      {
        steps: [
          { maxWeightGrams: 1000, price: 600 },
          { maxWeightGrams: 5000, price: 900 },
          { maxWeightGrams: 500, price: 450 },
        ],
      },
    ]);
    // The server sorted the steps, and the form shows them in that order.
    expect(
      within(screen.getByRole('group', { name: 'Стъпка 1' })).getByLabelText('Тегло до (грама)'),
    ).toHaveValue('500');
  });

  it('puts the focus on the new row, and on the add button after a row is removed', async () => {
    fakeApi();
    const user = userEvent.setup();
    renderPage();
    await loaded();

    await user.click(screen.getByRole('button', { name: 'Добави стъпка' }));
    expect(
      within(screen.getByRole('group', { name: 'Стъпка 3' })).getByLabelText('Тегло до (грама)'),
    ).toHaveFocus();
    await user.click(screen.getByRole('button', { name: 'Махни стъпка 3' }));

    expect(screen.getByRole('button', { name: 'Добави стъпка' })).toHaveFocus();
    expect(screen.queryByRole('group', { name: 'Стъпка 3' })).not.toBeInTheDocument();
  });

  it('names the wrong field next to it, moves the focus to it, and sends nothing', async () => {
    const server = fakeApi();
    const user = userEvent.setup();
    renderPage();
    await loaded();
    const first = screen.getByRole('group', { name: 'Стъпка 1' });
    await user.clear(within(first).getByLabelText('Цена (EUR)'));
    await user.type(within(first).getByLabelText('Цена (EUR)'), 'free');

    await save(user);

    const price = within(first).getByLabelText('Цена (EUR)');
    expect(price).toHaveAttribute('aria-invalid', 'true');
    expect(price).toHaveAccessibleDescription('Въведи цена, например 6.00.');
    expect(price).toHaveFocus();
    expect(server.puts).toEqual([]);
  });

  it('marks a repeated weight', async () => {
    const server = fakeApi();
    const user = userEvent.setup();
    renderPage();
    await loaded();
    const second = screen.getByRole('group', { name: 'Стъпка 2' });
    await user.clear(within(second).getByLabelText('Тегло до (грама)'));
    await user.type(within(second).getByLabelText('Тегло до (грама)'), '1000');

    await save(user);

    expect(within(second).getByLabelText('Тегло до (грама)')).toHaveAccessibleDescription(
      'Има друга стъпка със същото тегло.',
    );
    expect(server.puts).toEqual([]);
  });

  it('asks before it switches the delivery off, and sends an empty tariff only when the answer is yes', async () => {
    const server = fakeApi({ steps: [{ maxWeightGrams: 1000, price: 600 }] });
    const user = userEvent.setup();
    renderPage();
    await loaded();
    await user.click(screen.getByRole('button', { name: 'Махни стъпка 1' }));
    expect(screen.getByText(/Няма нито една стъпка/)).toBeInTheDocument();

    await save(user);
    const dialog = await screen.findByRole('dialog', { name: 'Да спре ли доставката?' });
    await user.click(within(dialog).getByRole('button', { name: 'Назад' }));
    expect(server.puts).toEqual([]);

    await save(user);
    const again = await screen.findByRole('dialog', { name: 'Да спре ли доставката?' });
    await user.click(within(again).getByRole('button', { name: 'Спри доставката' }));

    await waitFor(() => {
      expect(server.puts).toEqual([{ steps: [] }]);
    });
  });

  it('says a general thing, not what the server said, when the save fails', async () => {
    fakeApi({ reject: Response.json({ code: 'internal.secret_detail' }, { status: 500 }) });
    const user = userEvent.setup();
    renderPage();
    await loaded();

    await save(user);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Тарифата не можа да се запази');
    expect(alert).not.toHaveTextContent('secret_detail');
  });

  it('answers a concurrent save with a way to load the tariff again', async () => {
    fakeApi({ reject: Response.json({ code: 'shipping.tariff_conflict' }, { status: 409 }) });
    const user = userEvent.setup();
    renderPage();
    await loaded();

    await save(user);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Някой друг запази тарифата в същия момент');
    expect(within(alert).getByRole('button', { name: 'Презареди тарифата' })).toBeInTheDocument();
  });
});
