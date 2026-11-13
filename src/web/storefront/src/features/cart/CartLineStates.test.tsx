import { fireEvent, screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { findAccessibilityViolations } from '../../testing/axe';
import { fakeServer } from '../../testing/fakeCartServer';
import {
  cartLineFixture,
  categoriesFixture,
  productDetailsFixture,
  renderApp,
  settingsFixture,
  stubNetwork,
} from '../../testing/renderApp';

function stub(server: ReturnType<typeof fakeServer>) {
  return stubNetwork({
    settings: () => Response.json(settingsFixture()),
    categories: () => Response.json(categoriesFixture()),
    product: () => Response.json(productDetailsFixture()),
    cart: server.cart,
    config: { imageBaseUrl: 'http://img.test' },
  });
}

async function openPanel(): Promise<HTMLElement> {
  fireEvent.click(await screen.findByRole('button', { name: /^Количка, \d/ }));
  return screen.findByRole('dialog', { name: 'Количка' });
}

/** What the panel shows for a line, by how it is fulfilled and by the state the server gives it (task 21). */
describe('lines of the cart by state', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows a note for a line that cannot be bought as it is, and does not let the number grow', async () => {
    stub(
      fakeServer({
        initial: [
          cartLineFixture({ state: 'InsufficientStock' }),
          cartLineFixture({ variantId: 'v2', productName: 'Puzzle', sku: 'PZL-L' }),
        ],
      }),
    );
    renderApp('/products/dragon');
    const panel = await openPanel();

    expect(within(panel).getByText('Няма достатъчно бройки от този артикул.')).toBeInTheDocument();
    expect(within(panel).getByRole('button', { name: /^Увеличи .*Dragon/ })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
  });

  it('shows the way of making and the lead time of a made-to-order line, and one note for the cart', async () => {
    stub(
      fakeServer({
        initial: [
          cartLineFixture({
            fulfilmentType: 'MadeToOrder',
            leadTimeDays: 7,
            availableQuantity: null,
          }),
        ],
      }),
    );
    renderApp('/products/dragon');
    const panel = await openPanel();

    expect(within(panel).getByText('По поръчка · 7 дни')).toBeInTheDocument();
    expect(
      within(panel).getByText('Част от поръчката се изработва след плащане.'),
    ).toBeInTheDocument();
  });

  it('shows only "Made to order" when the shop gave no lead time', async () => {
    stub(
      fakeServer({
        initial: [
          cartLineFixture({
            fulfilmentType: 'MadeToOrder',
            leadTimeDays: null,
            availableQuantity: null,
          }),
        ],
      }),
    );
    renderApp('/products/dragon');
    const panel = await openPanel();
    expect(within(panel).getByText('По поръчка')).toBeInTheDocument();
  });

  it('shows no way of making and no note for a line that is in stock', async () => {
    stub(fakeServer({ initial: [cartLineFixture()] }));
    renderApp('/products/dragon');
    const panel = await openPanel();

    expect(within(panel).queryByText(/По поръчка/)).not.toBeInTheDocument();
    expect(within(panel).queryByText(/след плащане/)).not.toBeInTheDocument();
  });

  it('stops the stepper at the stock, below 20', async () => {
    stub(fakeServer({ initial: [cartLineFixture({ quantity: 3, availableQuantity: 3 })] }));
    renderApp('/products/dragon');
    const panel = await openPanel();

    expect(within(panel).getByRole('button', { name: /^Увеличи .*Dragon/ })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
  });

  it.each(['Inactive', 'Archived'])(
    'keeps a %s line in the cart with a note, no "+", and a "Remove" that works',
    async (state) => {
      const server = fakeServer({ initial: [cartLineFixture({ state })] });
      stub(server);
      renderApp('/products/dragon');
      const panel = await openPanel();

      expect(
        within(panel).getByText('Този артикул вече не се продава. Махни го от количката.'),
      ).toBeInTheDocument();
      expect(within(panel).getByRole('button', { name: /^Увеличи .*Dragon/ })).toHaveAttribute(
        'aria-disabled',
        'true',
      );
      fireEvent.click(within(panel).getByRole('button', { name: /^Махни .*Dragon/ }));
      expect(await within(panel).findByText('Количката е празна')).toBeInTheDocument();
    },
  );

  it('has no accessibility violations with a line in every state, made to order too', async () => {
    const states = ['InsufficientStock', 'Inactive', 'Archived'];
    stub(
      fakeServer({
        initial: [
          cartLineFixture({ variantId: 'a', sku: 'A' }),
          cartLineFixture({
            variantId: 'm',
            sku: 'M',
            fulfilmentType: 'MadeToOrder',
            leadTimeDays: 7,
            availableQuantity: null,
          }),
          ...states.map((state) => cartLineFixture({ variantId: state, sku: state, state })),
        ],
      }),
    );
    renderApp('/products/dragon');
    const panel = await openPanel();
    await within(panel).findByText('По поръчка · 7 дни');

    expect(await findAccessibilityViolations()).toEqual([]);
  });
});
