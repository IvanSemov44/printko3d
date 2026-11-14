import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { findAccessibilityViolations } from '../../testing/axe';
import {
  cartFixture,
  cartLineFixture,
  categoriesFixture,
  productDetailsFixture,
  renderApp,
  settingsFixture,
  stubNetwork,
  variantFixture,
} from '../../testing/renderApp';
import { fakeServer } from '../../testing/fakeCartServer';
import { plain } from '../../testing/text';

function stub(server: ReturnType<typeof fakeServer>, product: Record<string, unknown> = {}) {
  return stubNetwork({
    settings: () => Response.json(settingsFixture()),
    categories: () => Response.json(categoriesFixture()),
    product: () => Response.json(productDetailsFixture(product)),
    cart: server.cart,
    config: { imageBaseUrl: 'http://img.test' },
  });
}

function header(): HTMLElement {
  return screen.getByRole('banner');
}

async function findPanel(): Promise<HTMLElement> {
  return screen.findByRole('dialog', { name: 'Количка' });
}

describe('cart in the shop', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe('button in the header', () => {
    it('is named "empty" for a new visitor and shows no number', async () => {
      stub(fakeServer());
      renderApp('/products/dragon');

      const button = await within(await screen.findByRole('banner')).findByRole('button', {
        name: 'Количка, празна',
      });
      expect(button.textContent).toBe('');
    });

    it('shows the number of pieces the server counted, with the plural form in its name', async () => {
      stub(fakeServer({ initial: [cartLineFixture({ quantity: 3, linePrice: 3750 })] }));
      renderApp('/products/dragon');

      const button = await within(await screen.findByRole('banner')).findByRole('button', {
        name: 'Количка, 3 артикула',
      });
      expect(button).toHaveTextContent('3');
    });

    it('shows "99+" above 99', async () => {
      stub(fakeServer({ initial: [cartLineFixture({ quantity: 100, linePrice: 125000 })] }));
      renderApp('/products/dragon');

      const button = await within(await screen.findByRole('banner')).findByRole('button', {
        name: /Количка, 100/,
      });
      expect(button).toHaveTextContent('99+');
    });

    it('opens the panel', async () => {
      stub(fakeServer());
      renderApp('/products/dragon');

      fireEvent.click(await screen.findByRole('button', { name: 'Количка, празна' }));

      const panel = await findPanel();
      expect(within(panel).getByText('Количката е празна')).toBeInTheDocument();
    });
  });

  describe('add button', () => {
    it('adds the chosen variant with the header against cross-site requests, opens the panel and counts', async () => {
      const server = fakeServer();
      stub(server, {
        variants: [variantFixture({ id: 'v1', sku: 'DRG-S', name: 'Small', price: 1250 })],
      });
      renderApp('/products/dragon');

      fireEvent.click(await screen.findByRole('button', { name: 'Добави в количката' }));

      const panel = await findPanel();
      expect(await within(panel).findByText('Dragon')).toBeInTheDocument();
      expect(plain(panel.querySelector('.cart-subtotal strong')?.textContent ?? '')).toContain(
        '12,50',
      );
      const post = server.requests.find((request) => request.method === 'POST');
      expect(post?.url).toBe('/api/v1/cart/items');
      expect(post?.body).toEqual({ variantId: 'v1', quantity: 1 });
      expect(post?.headers.get('X-Requested-With')).toBe('XMLHttpRequest');
      expect(
        within(header()).getByRole('button', { name: 'Количка, 1 артикул' }),
      ).toBeInTheDocument();
    });

    it('is off for a sold-out variant and says why', async () => {
      stub(fakeServer(), {
        variants: [variantFixture({ isAvailable: false })],
      });
      renderApp('/products/dragon');

      const button = await screen.findByRole('button', { name: 'Добави в количката' });

      expect(button).toBeDisabled();
      expect(button).toHaveAccessibleDescription('Този вариант е изчерпан и не може да се добави.');
    });

    it('stays on for a made-to-order variant that has nothing in stock', async () => {
      stub(fakeServer(), {
        fulfilmentType: 'MadeToOrder',
        leadTimeDays: 5,
        variants: [variantFixture({ isAvailable: false })],
      });
      renderApp('/products/dragon');

      expect(await screen.findByRole('button', { name: 'Добави в количката' })).toBeEnabled();
    });

    it.each([
      [
        'cart.quantity_too_high',
        'Достигнат е най-големият брой от този артикул, който може да се добави.',
        422,
      ],
      [
        'cart.too_many_items',
        'В количката има твърде много различни артикули. Махни някой, за да добавиш нов.',
        422,
      ],
      ['variant.not_found', 'Този вариант вече не се продава.', 404],
    ])('says what is wrong for %s and keeps the panel closed', async (code, text, status) => {
      stub(fakeServer({ failWith: Response.json({ code }, { status }) }));
      renderApp('/products/dragon');

      fireEvent.click(await screen.findByRole('button', { name: 'Добави в количката' }));

      expect(await screen.findByRole('alert')).toHaveTextContent(text);
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('gives a general text, never the words of the server, for any other failure', async () => {
      stub(
        fakeServer({
          failWith: Response.json(
            { code: 'db.exploded', detail: 'secret detail' },
            { status: 500 },
          ),
        }),
      );
      renderApp('/products/dragon');

      fireEvent.click(await screen.findByRole('button', { name: 'Добави в количката' }));

      const alert = await screen.findByRole('alert');
      expect(alert).toHaveTextContent('Количката не можа да се обнови');
      expect(alert).not.toHaveTextContent('secret');
    });

    it('can be pressed again after a failure', async () => {
      const server = fakeServer({ failWith: Response.json({ code: 'x' }, { status: 500 }) });
      stub(server);
      renderApp('/products/dragon');

      const button = await screen.findByRole('button', { name: 'Добави в количката' });
      fireEvent.click(button);
      await screen.findByRole('alert');
      fireEvent.click(button);

      await waitFor(() => {
        expect(server.requests.filter((request) => request.method === 'POST')).toHaveLength(2);
      });
    });
  });

  describe('panel', () => {
    const twoLines = [
      cartLineFixture({ variantId: 'v1', quantity: 2, linePrice: 2500 }),
      cartLineFixture({
        variantId: 'v2',
        productName: 'Puzzle',
        productSlug: 'puzzle',
        variantName: 'Large',
        sku: 'PZL-L',
        unitPrice: 2000,
        linePrice: 2000,
        image: { storageKey: 'demo/puzzle.jpg', altText: 'Puzzle' },
      }),
    ];

    async function openPanel(): Promise<HTMLElement> {
      fireEvent.click(await screen.findByRole('button', { name: /^Количка, \d/ }));
      return findPanel();
    }

    it('shows the lines, the picture from the image address and the sum of the server', async () => {
      stub(fakeServer({ initial: twoLines }));
      renderApp('/products/dragon');

      const panel = await openPanel();

      expect(within(panel).getAllByRole('listitem')).toHaveLength(2);
      expect(within(panel).getByText('Puzzle')).toBeInTheDocument();
      expect(panel.querySelector('img')).toHaveAttribute('src', 'http://img.test/demo/puzzle.jpg');
      expect(plain(panel.querySelector('.cart-subtotal strong')?.textContent ?? '')).toContain(
        '45,00',
      );
      expect(
        within(panel).getByText('Цената на доставката се вижда при поръчка.'),
      ).toBeInTheDocument();
      expect(within(panel).getByRole('link', { name: 'Виж количката' })).toHaveAttribute(
        'href',
        '/cart',
      );
      expect(within(panel).getByRole('link', { name: 'Към поръчката' })).toHaveAttribute(
        'href',
        '/checkout',
      );
    });

    it('changes a quantity with the stepper and shows the answer of the server', async () => {
      const server = fakeServer({ initial: twoLines });
      stub(server);
      renderApp('/products/dragon');
      const panel = await openPanel();

      fireEvent.click(within(panel).getByRole('button', { name: /^Увеличи .*Dragon/ }));

      await waitFor(() => {
        expect(
          within(panel)
            .getByRole('group', { name: /Dragon/ })
            .querySelector('output'),
        ).toHaveTextContent('3');
      });
      const put = server.requests.find((request) => request.method === 'PUT');
      expect(put?.url).toBe('/api/v1/cart/items/v1');
      expect(put?.body).toEqual({ quantity: 3 });
      expect(put?.headers.get('X-Requested-With')).toBe('XMLHttpRequest');
      expect(plain(panel.querySelector('.cart-subtotal strong')?.textContent ?? '')).toContain(
        '57,50',
      );
    });

    it('does not go below 1 and does not go above 20', async () => {
      const server = fakeServer({
        initial: [
          cartLineFixture({ variantId: 'v1', quantity: 1 }),
          cartLineFixture({
            variantId: 'v2',
            productName: 'Puzzle',
            quantity: 20,
            linePrice: 25000,
          }),
        ],
      });
      stub(server);
      renderApp('/products/dragon');
      const panel = await openPanel();

      fireEvent.click(within(panel).getByRole('button', { name: /^Намали .*Dragon/ }));
      fireEvent.click(within(panel).getByRole('button', { name: /^Увеличи .*Puzzle/ }));

      expect(within(panel).getByRole('button', { name: /^Намали .*Dragon/ })).toHaveAttribute(
        'aria-disabled',
        'true',
      );
      expect(within(panel).getByRole('button', { name: /^Увеличи .*Puzzle/ })).toHaveAttribute(
        'aria-disabled',
        'true',
      );
      expect(server.requests.filter((request) => request.method === 'PUT')).toHaveLength(0);
    });

    it('sends one request at a time for a line', async () => {
      let answer: (response: Response) => void = () => undefined;
      const server = fakeServer({ initial: twoLines });
      stubNetwork({
        settings: () => Response.json(settingsFixture()),
        categories: () => Response.json(categoriesFixture()),
        product: () => Response.json(productDetailsFixture()),
        cart: (url, init) =>
          init?.method === 'PUT'
            ? new Promise<Response>((resolve) => {
                answer = resolve;
              })
            : server.cart(url, init),
      });
      renderApp('/products/dragon');
      const panel = await openPanel();
      const more = within(panel).getByRole('button', { name: /^Увеличи .*Dragon/ });

      fireEvent.click(more);
      await waitFor(() => {
        expect(more).toHaveAttribute('aria-disabled', 'true');
      });
      fireEvent.click(more);
      answer(Response.json(cartFixture(twoLines)));

      await waitFor(() => {
        expect(more).toHaveAttribute('aria-disabled', 'false');
      });
      expect(vi.mocked(fetch).mock.calls.filter(([, init]) => init?.method === 'PUT')).toHaveLength(
        1,
      );
    });

    it('removes a line and shows the empty cart after the last one', async () => {
      const server = fakeServer({ initial: [twoLines[0] as Record<string, unknown>] });
      stub(server);
      renderApp('/products/dragon');
      const panel = await openPanel();

      fireEvent.click(within(panel).getByRole('button', { name: /^Махни .*Dragon/ }));

      expect(await within(panel).findByText('Количката е празна')).toBeInTheDocument();
      const remove = server.requests.find((request) => request.method === 'DELETE');
      expect(remove?.url).toBe('/api/v1/cart/items/v1');
      expect(remove?.headers.get('X-Requested-With')).toBe('XMLHttpRequest');
    });

    it('says so, with a retry, when the cart cannot be read', async () => {
      stubNetwork({
        settings: () => Response.json(settingsFixture()),
        categories: () => Response.json(categoriesFixture()),
        product: () => Response.json(productDetailsFixture()),
        cart: () => Response.json({}, { status: 500 }),
      });
      renderApp('/products/dragon');

      fireEvent.click(await screen.findByRole('button', { name: 'Количка, празна' }));

      const panel = await findPanel();
      expect(await within(panel).findByRole('alert')).toHaveTextContent(
        'Количката не може да се зареди в момента',
      );
      expect(within(panel).getByRole('button', { name: 'Опитай пак' })).toBeInTheDocument();
    });

    it.each([
      [
        'the close button',
        (panel: HTMLElement) => {
          fireEvent.click(within(panel).getByRole('button', { name: 'Затвори количката' }));
        },
      ],
      [
        'Esc',
        (panel: HTMLElement) => {
          fireEvent(panel, new Event('cancel', { cancelable: true }));
        },
      ],
      [
        'a click on the dark area',
        (panel: HTMLElement) => {
          fireEvent.click(panel);
        },
      ],
    ])(
      'closes with %s and gives the focus back to the control that opened it',
      async (_name, close) => {
        stub(fakeServer({ initial: twoLines }));
        renderApp('/products/dragon');
        const opener = await screen.findByRole('button', { name: /^Количка, \d/ });
        opener.focus();
        fireEvent.click(opener);
        const panel = await findPanel();
        // The focus is inside the panel while it is open, as it is in a browser.
        within(panel).getByRole('button', { name: 'Затвори количката' }).focus();
        expect(opener).not.toHaveFocus();

        close(panel);

        expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
        expect(opener).toHaveFocus();
      },
    );

    it('closes when the buyer follows a link, and the page changes', async () => {
      stub(fakeServer({ initial: twoLines }));
      renderApp('/products/dragon');
      const panel = await openPanel();

      fireEvent.click(within(panel).getByRole('link', { name: 'Puzzle' }));

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      expect(screen.getByTestId('location')).toHaveTextContent('/products/puzzle?variant=PZL-L');
    });

    it('has no accessibility violations with the panel open', async () => {
      stub(fakeServer({ initial: twoLines }));
      renderApp('/products/dragon');
      const panel = await openPanel();
      await within(panel).findByText('Puzzle');

      expect(await findAccessibilityViolations()).toEqual([]);
    });
  });

  it('does not build the panel before somebody opens it', async () => {
    stub(fakeServer());
    renderApp('/products/dragon');

    await screen.findByRole('button', { name: 'Количка, празна' });

    expect(document.querySelector('.cart-panel')).toBeNull();
  });
});
