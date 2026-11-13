import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { findAccessibilityViolations } from '../../testing/axe';
import { fakeServer } from '../../testing/fakeCartServer';
import { orderFixture } from '../../testing/orderFixture';
import {
  cartLineFixture,
  categoriesFixture,
  findContent,
  renderApp,
  settingsFixture,
  stubNetwork,
} from '../../testing/renderApp';
import { plain } from '../../testing/text';

const NUMBER = 12345678;

function stub(server = fakeServer(), orders?: () => Response) {
  return stubNetwork({
    settings: () => Response.json(settingsFixture()),
    categories: () => Response.json(categoriesFixture()),
    cart: server.cart,
    ...(orders ? { orders } : {}),
  });
}

async function open(state: unknown, number = String(NUMBER)) {
  stub();
  renderApp({ pathname: `/order/${number}`, state });
  return findContent();
}

function robots(): string | null {
  return document.head.querySelector('meta[name="robots"]')?.getAttribute('content') ?? null;
}

describe('order confirmation page', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the number, the lines, the total, the address and that payment is not there yet', async () => {
    const page = await open({
      order: orderFixture({
        items: [
          {
            productName: 'Dragon',
            variantName: 'Small',
            sku: 'DRG-S',
            unitPrice: 1250,
            quantity: 2,
            lineTotal: 2500,
            fulfilmentType: 'InStock',
            leadTimeDays: null,
          },
          {
            productName: 'Puzzle',
            variantName: 'Large',
            sku: 'PZL-L',
            unitPrice: 2000,
            quantity: 1,
            lineTotal: 2000,
            fulfilmentType: 'MadeToOrder',
            leadTimeDays: 7,
          },
        ],
        total: 4500,
      }),
    });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Поръчката е приета' }),
    ).toBeInTheDocument();
    expect(within(page).getByText('PR-12345678')).toBeInTheDocument();
    expect(within(page).getAllByRole('listitem')).toHaveLength(2);
    expect(within(page).getByText('Dragon')).toBeInTheDocument();
    expect(within(page).getByText(/По поръчка · 7 дни/)).toBeInTheDocument();
    expect(plain(page.querySelector('.checkout-total strong')?.textContent ?? '')).toContain(
      '45,00',
    );
    expect(within(page).getByText('Test Buyer')).toBeInTheDocument();
    expect(within(page).getByText(/1 Test Street/)).toBeInTheDocument();
    expect(within(page).getByText(/Плащането още не е включено/)).toBeInTheDocument();
    expect(within(page).getByRole('link', { name: 'Към началото' })).toHaveAttribute('href', '/');
  });

  it('is kept out of search results and has its own title', async () => {
    await open({ order: orderFixture() });

    await screen.findByRole('heading', { level: 1, name: 'Поръчката е приета' });

    expect(robots()).toBe('noindex');
    expect(document.title).toBe('Поръчката е приета – Test Shop');
  });

  it.each([
    ['there is no state (the address was opened fresh)', undefined],
    ['the state is not an order', { order: { number: 'x' } }],
    ['the state has no order', {}],
    ['the order has the right number but no lines', { order: { number: NUMBER } }],
    [
      'the order is another one than the address says',
      { order: orderFixture({ number: 87654321 }) },
    ],
  ])('shows only a short message, and no order data, when %s', async (_name, state) => {
    const page = await open(state);

    expect(await screen.findByRole('heading', { level: 1, name: 'Поръчка' })).toBeInTheDocument();
    expect(
      within(page).getByText(/се показват само веднага след потвърждението/),
    ).toBeInTheDocument();
    expect(within(page).getByRole('link', { name: 'Към началото' })).toHaveAttribute('href', '/');
    expect(within(page).queryByText('Test Buyer')).not.toBeInTheDocument();
    expect(within(page).queryByText(/PR-/)).not.toBeInTheDocument();
    expect(robots()).toBe('noindex');
  });

  it('is reached from the checkout, with the cart empty on the page and in the header', async () => {
    const server = fakeServer({
      initial: [cartLineFixture({ variantId: 'v1', quantity: 2, linePrice: 2500 })],
    });
    stub(server, () => Response.json(orderFixture(), { status: 201 }));
    const user = userEvent.setup();
    renderApp('/checkout');
    await screen.findByRole('button', { name: 'Потвърди поръчката' });

    for (const [label, value] of [
      ['Име и фамилия', 'Test Buyer'],
      ['Имейл', 'buyer@example.test'],
      ['Телефон', '+359 88 000 0000'],
      ['Улица и номер', '1 Test Street'],
      ['Град', 'Sofia'],
      ['Пощенски код', '1000'],
    ] as const) {
      await user.type(screen.getByLabelText(label), value);
    }
    await user.click(screen.getByRole('checkbox', { name: 'Съгласен съм с Общите условия' }));
    await user.click(screen.getByRole('button', { name: 'Потвърди поръчката' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Поръчката е приета' }),
    ).toBeInTheDocument();
    expect(screen.getByText('PR-12345678')).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Количка, празна' })).toBeInTheDocument();
    });
  });

  it('has no accessibility violations, with the order and with the short message', async () => {
    await open({ order: orderFixture() });
    await screen.findByRole('heading', { level: 1, name: 'Поръчката е приета' });
    expect(await findAccessibilityViolations()).toEqual([]);
  });

  it('has no accessibility violations in the short message either', async () => {
    await open(undefined);
    await screen.findByRole('heading', { level: 1, name: 'Поръчка' });
    expect(await findAccessibilityViolations()).toEqual([]);
  });
});
