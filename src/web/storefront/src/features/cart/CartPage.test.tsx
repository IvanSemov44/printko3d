import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { findAccessibilityViolations } from '../../testing/axe';
import { fakeServer } from '../../testing/fakeCartServer';
import {
  cartLineFixture,
  categoriesFixture,
  findContent,
  productDetailsFixture,
  renderApp,
  settingsFixture,
  stubNetwork,
} from '../../testing/renderApp';
import { plain } from '../../testing/text';

const LINES = [
  cartLineFixture({ variantId: 'v1', quantity: 2, linePrice: 2500 }),
  cartLineFixture({
    variantId: 'v2',
    productName: 'Puzzle',
    productSlug: 'puzzle',
    variantName: 'Large',
    sku: 'PZL-L',
    unitPrice: 2000,
    linePrice: 2000,
  }),
];

function stub(server: ReturnType<typeof fakeServer>) {
  return stubNetwork({
    settings: () => Response.json(settingsFixture()),
    categories: () => Response.json(categoriesFixture()),
    product: () => Response.json(productDetailsFixture()),
    cart: server.cart,
  });
}

function robots(): string | null {
  return document.head.querySelector('meta[name="robots"]')?.getAttribute('content') ?? null;
}

function sum(page: HTMLElement): string {
  return plain(page.querySelector('.cart-summary strong')?.textContent ?? '');
}

describe('cart page', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the lines and the sum of the server, and has no way to pay yet', async () => {
    stub(fakeServer({ initial: LINES }));
    renderApp('/cart');

    expect(await screen.findByRole('heading', { level: 1, name: 'Количка' })).toBeInTheDocument();
    const page = await findContent();
    expect(await within(page).findAllByRole('listitem')).toHaveLength(2);
    expect(sum(page)).toContain('45,00');
    expect(within(page).getByText('Доставката се изчислява при плащане.')).toBeInTheDocument();
    expect(within(page).queryByRole('link', { name: /плащане/i })).not.toBeInTheDocument();
  });

  it('is kept out of search results and has its own title', async () => {
    stub(fakeServer({ initial: LINES }));
    renderApp('/cart');

    await screen.findByRole('heading', { level: 1, name: 'Количка' });

    expect(robots()).toBe('noindex');
    expect(document.title).toBe('Количка – Test Shop');
  });

  it('says that the cart is empty and offers a way on', async () => {
    stub(fakeServer());
    renderApp('/cart');

    const page = await findContent();
    expect(await within(page).findByText('Количката е празна')).toBeInTheDocument();
    expect(within(page).getByRole('link', { name: 'Разгледай продуктите' })).toHaveAttribute(
      'href',
      '/',
    );
  });

  it('says so, with a retry, when the cart cannot be read', async () => {
    let failing = true;
    const server = fakeServer({ initial: LINES });
    stubNetwork({
      settings: () => Response.json(settingsFixture()),
      categories: () => Response.json(categoriesFixture()),
      cart: (url, init) => (failing ? Response.json({}, { status: 500 }) : server.cart(url, init)),
    });
    renderApp('/cart');

    const page = await findContent();
    expect(await within(page).findByRole('alert')).toHaveTextContent(
      'Количката не може да се зареди в момента',
    );
    failing = false;
    fireEvent.click(within(page).getByRole('button', { name: 'Опитай пак' }));

    expect(await within(page).findAllByRole('listitem')).toHaveLength(2);
  });

  it('changes a quantity and shows the new sum from the server', async () => {
    const server = fakeServer({ initial: LINES });
    stub(server);
    renderApp('/cart');
    const page = await findContent();

    fireEvent.click(await within(page).findByRole('button', { name: /^Увеличи .*Puzzle/ }));

    await waitFor(() => {
      expect(sum(page)).toContain('65,00');
    });
    const put = server.requests.find((request) => request.method === 'PUT');
    expect(put?.url).toBe('/api/v1/cart/items/v2');
    expect(put?.headers.get('X-Requested-With')).toBe('XMLHttpRequest');
  });

  describe('clearing the cart', () => {
    it('asks first, and "No" sends nothing and gives the focus back to the first button', async () => {
      const server = fakeServer({ initial: LINES });
      stub(server);
      renderApp('/cart');
      const page = await findContent();

      fireEvent.click(await within(page).findByRole('button', { name: 'Изчисти количката' }));

      expect(within(page).getByText('Да изчистим ли цялата количка?')).toBeInTheDocument();
      const no = within(page).getByRole('button', { name: 'Не, остави я' });
      expect(no).toHaveFocus();
      fireEvent.click(no);
      expect(within(page).getByRole('button', { name: 'Изчисти количката' })).toHaveFocus();
      expect(server.requests.filter((request) => request.method === 'DELETE')).toHaveLength(0);
      expect(within(page).getAllByRole('listitem')).toHaveLength(2);
    });

    it('empties the cart after "Yes", with the header against cross-site requests', async () => {
      const server = fakeServer({ initial: LINES });
      stub(server);
      renderApp('/cart');
      const page = await findContent();

      fireEvent.click(await within(page).findByRole('button', { name: 'Изчисти количката' }));
      fireEvent.click(within(page).getByRole('button', { name: 'Да, изчисти' }));

      expect(await within(page).findByText('Количката е празна')).toBeInTheDocument();
      const clear = server.requests.find((request) => request.method === 'DELETE');
      expect(clear?.url).toBe('/api/v1/cart');
      expect(clear?.headers.get('X-Requested-With')).toBe('XMLHttpRequest');
      expect(
        within(screen.getByRole('banner')).getByRole('link', { name: 'Количка, празна' }),
      ).toBeInTheDocument();
    });

    it('sends one request even if "Yes" is pressed twice', async () => {
      let answer: (response: Response) => void = () => undefined;
      const server = fakeServer({ initial: LINES });
      const deletes: string[] = [];
      stubNetwork({
        settings: () => Response.json(settingsFixture()),
        categories: () => Response.json(categoriesFixture()),
        cart: (url, init) => {
          if (init?.method === 'DELETE') {
            deletes.push(url);
            return new Promise<Response>((resolve) => {
              answer = resolve;
            });
          }
          return server.cart(url, init);
        },
      });
      renderApp('/cart');
      const page = await findContent();
      fireEvent.click(await within(page).findByRole('button', { name: 'Изчисти количката' }));
      const yes = within(page).getByRole('button', { name: 'Да, изчисти' });

      fireEvent.click(yes);
      await within(page).findByRole('button', { name: 'Изчиства се…' });
      fireEvent.click(yes);
      answer(Response.json({ lines: [], itemCount: 0, total: 0 }));

      expect(await within(page).findByText('Количката е празна')).toBeInTheDocument();
      expect(deletes).toHaveLength(1);
    });

    it('says that it failed, in general words, and keeps the lines', async () => {
      stub(
        fakeServer({
          initial: LINES,
          failWith: Response.json({ code: 'db.exploded', detail: 'secret' }, { status: 500 }),
        }),
      );
      renderApp('/cart');
      const page = await findContent();

      fireEvent.click(await within(page).findByRole('button', { name: 'Изчисти количката' }));
      fireEvent.click(within(page).getByRole('button', { name: 'Да, изчисти' }));

      const alert = await within(page).findByRole('alert');
      expect(alert).toHaveTextContent('Количката не можа да се обнови');
      expect(alert).not.toHaveTextContent('secret');
      expect(within(page).getAllByRole('listitem')).toHaveLength(2);
    });
  });

  describe('cart button in the header', () => {
    it('is a link to the page on the cart page, and a button that opens the panel elsewhere', async () => {
      stub(fakeServer({ initial: LINES }));
      renderApp('/cart');

      const link = await within(await screen.findByRole('banner')).findByRole('link', {
        name: 'Количка, 3 артикула',
      });

      expect(link).toHaveAttribute('href', '/cart');
      expect(link).toHaveAttribute('aria-current', 'page');
      fireEvent.click(link);
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('opens the panel on another page', async () => {
      stub(fakeServer({ initial: LINES }));
      renderApp('/products/dragon');

      fireEvent.click(await screen.findByRole('button', { name: 'Количка, 3 артикула' }));

      expect(await screen.findByRole('dialog', { name: 'Количка' })).toBeInTheDocument();
    });
  });

  describe('accessibility', () => {
    it('has no violations with lines', async () => {
      stub(fakeServer({ initial: LINES }));
      renderApp('/cart');
      await within(await findContent()).findAllByRole('listitem');

      expect(await findAccessibilityViolations()).toEqual([]);
    });

    it('has no violations when the cart is empty', async () => {
      stub(fakeServer());
      renderApp('/cart');
      await within(await findContent()).findByText('Количката е празна');

      expect(await findAccessibilityViolations()).toEqual([]);
    });

    it('has no violations while the question about clearing is shown', async () => {
      stub(fakeServer({ initial: LINES }));
      renderApp('/cart');
      const page = await findContent();
      fireEvent.click(await within(page).findByRole('button', { name: 'Изчисти количката' }));

      expect(await findAccessibilityViolations()).toEqual([]);
    });
  });
});
