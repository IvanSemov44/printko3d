import { fireEvent, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { findAccessibilityViolations } from '../../testing/axe';
import { fakeServer } from '../../testing/fakeCartServer';
import {
  cartFixture,
  cartLineFixture,
  categoriesFixture,
  productDetailsFixture,
  renderApp,
  settingsFixture,
  stubNetwork,
} from '../../testing/renderApp';
import { plain } from '../../testing/text';

const SEEN_KEY = 'printko-cart-seen';

function stub(cart: (url: string, init?: RequestInit) => Response | Promise<Response>) {
  return stubNetwork({
    settings: () => Response.json(settingsFixture()),
    categories: () => Response.json(categoriesFixture()),
    product: () => Response.json(productDetailsFixture()),
    cart,
  });
}

async function openPanel(): Promise<HTMLElement> {
  fireEvent.click(await screen.findByRole('button', { name: /^Количка, \d/ }));
  return screen.findByRole('dialog', { name: 'Количка' });
}

/** What the buyer was told about the last cart they saw (task 21c). */
describe('notices about a changed cart', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    sessionStorage.clear();
  });

  it('says nothing the first time the cart is seen, and keeps the cart as the base', async () => {
    stub(fakeServer({ initial: [cartLineFixture({ unitPrice: 1500 })] }).cart);
    renderApp('/products/dragon');

    const panel = await openPanel();

    expect(within(panel).queryByRole('button', { name: /известието/ })).not.toBeInTheDocument();
    expect(JSON.parse(sessionStorage.getItem(SEEN_KEY) ?? 'null')).toEqual({
      v1: { unitPrice: 1500, state: 'Available' },
    });
  });

  it('tells a price that changed since the last visit of the tab, with both prices', async () => {
    sessionStorage.setItem(
      SEEN_KEY,
      JSON.stringify({ v1: { unitPrice: 1250, state: 'Available' } }),
    );
    stub(fakeServer({ initial: [cartLineFixture({ unitPrice: 1500, linePrice: 1500 })] }).cart);
    renderApp('/products/dragon');

    const panel = await openPanel();

    const notice = await within(panel).findByText(/се промени от/);
    expect(plain(notice.textContent)).toContain(
      'Цената на „Dragon“ се промени от 12,50 € на 15,00 €',
    );
  });

  it('tells a price that changes while the buyer works with the cart', async () => {
    let price = 1250;
    stub((_url, init) => {
      if (init?.method === 'PUT') {
        price = 1400;
      }
      return Response.json(
        cartFixture([cartLineFixture({ quantity: 2, unitPrice: price, linePrice: price * 2 })]),
      );
    });
    renderApp('/products/dragon');
    const panel = await openPanel();
    await within(panel).findByText('Dragon');

    fireEvent.click(within(panel).getByRole('button', { name: /^Увеличи .*Dragon/ }));

    expect(await within(panel).findByText(/се промени от/)).toBeInTheDocument();
  });

  it('tells a line that can no longer be bought, and says what to do', async () => {
    sessionStorage.setItem(
      SEEN_KEY,
      JSON.stringify({ v1: { unitPrice: 1250, state: 'Available' } }),
    );
    stub(fakeServer({ initial: [cartLineFixture({ state: 'Archived' })] }).cart);
    renderApp('/products/dragon');

    const panel = await openPanel();

    expect(await within(panel).findByText(/„Dragon“ вече не се продава/)).toBeInTheDocument();
  });

  it('tells a line with too little stock', async () => {
    sessionStorage.setItem(
      SEEN_KEY,
      JSON.stringify({ v1: { unitPrice: 1250, state: 'Available' } }),
    );
    stub(fakeServer({ initial: [cartLineFixture({ state: 'InsufficientStock' })] }).cart);
    renderApp('/products/dragon');

    const panel = await openPanel();

    expect(await within(panel).findByText(/вече няма достатъчно бройки/)).toBeInTheDocument();
  });

  it('does not tell a line that is new in the cart', async () => {
    sessionStorage.setItem(
      SEEN_KEY,
      JSON.stringify({ other: { unitPrice: 100, state: 'Available' } }),
    );
    stub(fakeServer({ initial: [cartLineFixture({ unitPrice: 1500 })] }).cart);
    renderApp('/products/dragon');

    const panel = await openPanel();

    expect(within(panel).queryByText(/се промени от/)).not.toBeInTheDocument();
  });

  it('can be closed, and is not told again for the same change', async () => {
    sessionStorage.setItem(
      SEEN_KEY,
      JSON.stringify({ v1: { unitPrice: 1250, state: 'Available' } }),
    );
    stub(fakeServer({ initial: [cartLineFixture({ unitPrice: 1500 })] }).cart);
    renderApp('/products/dragon');
    const panel = await openPanel();
    await within(panel).findByText(/се промени от/);

    fireEvent.click(within(panel).getByRole('button', { name: 'Затвори известието за Dragon' }));

    expect(within(panel).queryByText(/се промени от/)).not.toBeInTheDocument();
    expect(JSON.parse(sessionStorage.getItem(SEEN_KEY) ?? 'null')).toEqual({
      v1: { unitPrice: 1500, state: 'Available' },
    });
  });

  it('is told on the cart page too, once for both places', async () => {
    sessionStorage.setItem(
      SEEN_KEY,
      JSON.stringify({ v1: { unitPrice: 1250, state: 'Available' } }),
    );
    stub(fakeServer({ initial: [cartLineFixture({ unitPrice: 1500 })] }).cart);
    renderApp('/cart');

    expect(await screen.findAllByText(/се промени от/)).toHaveLength(1);
  });

  it('keeps working when the storage is blocked: no notice, and no error', async () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    stub(fakeServer({ initial: [cartLineFixture()] }).cart);
    renderApp('/products/dragon');

    const panel = await openPanel();

    expect(await within(panel).findByText('Dragon')).toBeInTheDocument();
    vi.restoreAllMocks();
  });

  it('has no accessibility violations with a notice shown', async () => {
    sessionStorage.setItem(
      SEEN_KEY,
      JSON.stringify({ v1: { unitPrice: 1250, state: 'Available' } }),
    );
    stub(fakeServer({ initial: [cartLineFixture({ unitPrice: 1500 })] }).cart);
    renderApp('/products/dragon');
    const panel = await openPanel();
    await within(panel).findByText(/се промени от/);

    expect(await findAccessibilityViolations()).toEqual([]);
  });
});
