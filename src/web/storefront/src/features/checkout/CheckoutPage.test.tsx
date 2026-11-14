import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { findAccessibilityViolations } from '../../testing/axe';
import { fakeServer } from '../../testing/fakeCartServer';
import {
  cartLineFixture,
  categoriesFixture,
  findContent,
  renderApp,
  settingsFixture,
  stubNetwork,
} from '../../testing/renderApp';
import { orderFixture } from '../../testing/orderFixture';
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
    fulfilmentType: 'MadeToOrder',
    leadTimeDays: 7,
    availableQuantity: null,
  }),
];

type OrdersHandler = (url: string, init?: RequestInit) => Response | Promise<Response>;

function stub(server: ReturnType<typeof fakeServer>, orders?: OrdersHandler) {
  return stubNetwork({
    settings: () => Response.json(settingsFixture()),
    categories: () => Response.json(categoriesFixture()),
    cart: server.cart,
    ...(orders ? { orders } : {}),
  });
}

function problem(status: number, code: string, errors?: Record<string, string[]>) {
  return Response.json({ status, code, ...(errors ? { errors } : {}) }, { status });
}

/** Fills the form the way a buyer does, field by field. Leave a value out to leave the field empty. */
async function fill(
  user: ReturnType<typeof userEvent.setup>,
  values: Partial<
    Record<'name' | 'email' | 'phone' | 'street' | 'city' | 'postalCode', string>
  > = {},
) {
  const all = {
    name: 'Test Buyer',
    email: 'buyer@example.test',
    phone: '+359 88 000 0000',
    street: '1 Test Street',
    city: 'Sofia',
    postalCode: '1000',
    ...values,
  };
  const labels = {
    name: 'Име и фамилия',
    email: 'Имейл',
    phone: 'Телефон',
    street: 'Улица и номер',
    city: 'Град',
    postalCode: 'Пощенски код',
  } as const;
  for (const [field, label] of Object.entries(labels) as [keyof typeof labels, string][]) {
    if (all[field] !== '') {
      await user.type(screen.getByLabelText(label), all[field]);
    }
  }
}

async function openCheckout(server = fakeServer({ initial: LINES }), orders?: OrdersHandler) {
  stub(server, orders);
  renderApp('/checkout');
  await screen.findByRole('heading', { level: 1, name: 'Поръчка' });
  await screen.findByRole('button', { name: 'Потвърди поръчката' });
}

const acceptTerms = (user: ReturnType<typeof userEvent.setup>) =>
  user.click(screen.getByRole('checkbox', { name: 'Съгласен съм с Общите условия' }));

const submit = (user: ReturnType<typeof userEvent.setup>) =>
  user.click(screen.getByRole('button', { name: 'Потвърди поръчката' }));

function location(): string {
  return screen.getByTestId('location').textContent;
}

/** The JSON the page sent, read back. */
function bodyOf(init: RequestInit | undefined): unknown {
  return typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined;
}

function ordersSent(orders: ReturnType<typeof vi.fn>) {
  return orders.mock.calls.length;
}

describe('checkout page', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the lines, the made-to-order note and the sum of the server beside the form', async () => {
    await openCheckout();

    const page = await findContent();
    expect(within(page).getByText('Dragon')).toBeInTheDocument();
    expect(within(page).getByText('Puzzle')).toBeInTheDocument();
    expect(within(page).getByText(/По поръчка · 7 дни/)).toBeInTheDocument();
    expect(
      within(page).getByText('Част от поръчката се изработва след плащане.'),
    ).toBeInTheDocument();
    expect(plain(page.querySelector('.checkout-total strong')?.textContent ?? '')).toContain(
      '45,00',
    );
    expect(within(page).queryByRole('button', { name: /Махни|Изчисти/ })).not.toBeInTheDocument();
  });

  it('is kept out of search results and has its own title', async () => {
    await openCheckout();

    expect(document.head.querySelector('meta[name="robots"]')?.getAttribute('content')).toBe(
      'noindex',
    );
    expect(document.title).toBe('Поръчка – Test Shop');
  });

  it('sends a buyer with an empty cart back to the shop, without a form', async () => {
    stub(fakeServer());
    renderApp('/checkout');

    expect(await screen.findByText('Количката е празна')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Разгледай продуктите' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Потвърди поръчката' })).not.toBeInTheDocument();
  });

  it('names the empty fields next to them, moves the focus to the first, and sends nothing', async () => {
    const orders = vi.fn<OrdersHandler>();
    const user = userEvent.setup();
    await openCheckout(fakeServer({ initial: LINES }), orders);

    await submit(user);

    const name = screen.getByLabelText('Име и фамилия');
    expect(name).toHaveAttribute('aria-invalid', 'true');
    expect(name).toHaveFocus();
    expect(screen.getByLabelText('Имейл')).toHaveAccessibleDescription('Попълни полето.');
    expect(
      screen.getByRole('checkbox', { name: 'Съгласен съм с Общите условия' }),
    ).toHaveAccessibleDescription('Трябва да приемеш Общите условия, за да поръчаш.');
    expect(ordersSent(orders)).toBe(0);
  });

  it('explains a wrong email and a wrong postal code in plain words', async () => {
    const user = userEvent.setup();
    await openCheckout();

    await fill(user, { email: 'not-an-email', postalCode: '10' });
    await acceptTerms(user);
    await submit(user);

    expect(screen.getByLabelText('Имейл')).toHaveAccessibleDescription('Въведи валиден имейл.');
    expect(screen.getByLabelText('Пощенски код')).toHaveAccessibleDescription(
      'Пощенският код е четири цифри.',
    );
  });

  it('places the order with the total it showed, goes to the confirmation and empties the cart', async () => {
    const server = fakeServer({ initial: LINES });
    const orders = vi.fn<OrdersHandler>(() => Response.json(orderFixture(), { status: 201 }));
    const user = userEvent.setup();
    await openCheckout(server, orders);

    await fill(user);
    await acceptTerms(user);
    await submit(user);

    await waitFor(() => {
      expect(location()).toBe('/order/12345678 PUSH');
    });
    const [url, init] = orders.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/orders');
    expect(init?.method).toBe('POST');
    expect(new Headers(init?.headers).get('X-Requested-With')).toBe('XMLHttpRequest');
    expect(bodyOf(init)).toEqual({
      name: 'Test Buyer',
      email: 'buyer@example.test',
      phone: '+359 88 000 0000',
      street: '1 Test Street',
      city: 'Sofia',
      postalCode: '1000',
      termsAccepted: true,
      expectedTotal: 4500,
    });
  });

  it('trims the fields before it sends them', async () => {
    const orders = vi.fn<OrdersHandler>(() => Response.json(orderFixture(), { status: 201 }));
    const user = userEvent.setup();
    await openCheckout(fakeServer({ initial: LINES }), orders);

    await fill(user, { name: '  Test Buyer  ', city: ' Sofia ' });
    await acceptTerms(user);
    await submit(user);

    await waitFor(() => {
      expect(ordersSent(orders)).toBe(1);
    });
    expect(bodyOf((orders.mock.calls[0] ?? [])[1])).toMatchObject({
      name: 'Test Buyer',
      city: 'Sofia',
    });
  });

  it('sends one order when the button is pressed twice while it waits', async () => {
    let answer: (response: Response) => void = () => undefined;
    const orders = vi.fn<OrdersHandler>(
      () =>
        new Promise<Response>((resolve) => {
          answer = resolve;
        }),
    );
    const user = userEvent.setup();
    await openCheckout(fakeServer({ initial: LINES }), orders);
    await fill(user);
    await acceptTerms(user);

    await submit(user);
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Поръчката се изпраща…' })).toHaveAttribute(
        'aria-disabled',
        'true',
      );
    });
    await user.click(screen.getByRole('button', { name: 'Поръчката се изпраща…' }));
    answer(Response.json(orderFixture(), { status: 201 }));

    await waitFor(() => {
      expect(location()).toBe('/order/12345678 PUSH');
    });
    expect(ordersSent(orders)).toBe(1);
  });

  it.each([
    [
      'order.total_changed',
      409,
      'Цената в количката се е променила, откакто я видя. Прегледай я и потвърди пак.',
    ],
    [
      'order.cart_not_orderable',
      422,
      'Някой артикул вече не може да се поръча. Провери количката и го махни.',
    ],
    [
      'order.stock_changed',
      409,
      'Последните бройки от някой артикул току-що бяха взети от друг купувач. Провери количката и поръчай пак.',
    ],
    ['cart.changed', 409, 'Количката се промени в същия момент. Опитай пак.'],
  ])('answers %s with a notice, a way to the cart and a fresh cart', async (code, status, text) => {
    const server = fakeServer({ initial: LINES });
    const orders = vi.fn<OrdersHandler>(() => problem(status, code));
    const user = userEvent.setup();
    await openCheckout(server, orders);
    await fill(user);
    await acceptTerms(user);
    const cartReadsBefore = server.requests.filter((r) => r.method === 'GET').length;

    await submit(user);

    const notice = await screen.findByRole('alert');
    expect(notice).toHaveTextContent(text);
    // The buyer stays on the page: it is still the address the test opened.
    expect(location()).toBe('/checkout POP');
    if (code !== 'cart.changed') {
      expect(within(notice).getByRole('link', { name: 'Към количката' })).toHaveAttribute(
        'href',
        '/cart',
      );
    }
    await waitFor(() => {
      expect(server.requests.filter((r) => r.method === 'GET').length).toBeGreaterThan(
        cartReadsBefore,
      );
    });
    expect(screen.getByLabelText('Име и фамилия')).toHaveValue('Test Buyer');
  });

  it('sends the buyer to the cart when the server says it is empty', async () => {
    const orders = vi.fn<OrdersHandler>(() => problem(422, 'order.cart_empty'));
    const user = userEvent.setup();
    await openCheckout(fakeServer({ initial: LINES }), orders);
    await fill(user);
    await acceptTerms(user);

    await submit(user);

    await waitFor(() => {
      expect(location()).toBe('/cart REPLACE');
    });
  });

  it('marks the fields the server refused, next to them', async () => {
    const orders = vi.fn<OrdersHandler>(() =>
      problem(400, 'validation.failed', { email: ['bad'], phone: ['bad'] }),
    );
    const user = userEvent.setup();
    await openCheckout(fakeServer({ initial: LINES }), orders);
    await fill(user);
    await acceptTerms(user);

    await submit(user);

    await waitFor(() => {
      expect(screen.getByLabelText('Имейл')).toHaveAttribute('aria-invalid', 'true');
    });
    expect(screen.getByLabelText('Имейл')).toHaveAccessibleDescription('Провери това поле.');
    expect(screen.getByLabelText('Телефон')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByLabelText('Град')).not.toHaveAttribute('aria-invalid');
  });

  it('says a general thing, not what the server said, when anything else goes wrong', async () => {
    const orders = vi.fn<OrdersHandler>(() => problem(500, 'internal.secret_detail'));
    const user = userEvent.setup();
    await openCheckout(fakeServer({ initial: LINES }), orders);
    await fill(user);
    await acceptTerms(user);

    await submit(user);

    const notice = await screen.findByRole('alert');
    expect(notice).toHaveTextContent('Поръчката не можа да се запази. Опитай отново след малко.');
    expect(notice).not.toHaveTextContent('secret_detail');
    expect(screen.getByLabelText('Име и фамилия')).toHaveValue('Test Buyer');
  });

  it('does not let a buyer order a line that is no longer sold', async () => {
    const orders = vi.fn<OrdersHandler>();
    const user = userEvent.setup();
    await openCheckout(
      fakeServer({
        initial: [LINES[0] ?? {}, cartLineFixture({ variantId: 'v3', state: 'Archived' })],
      }),
      orders,
    );
    await fill(user);
    await acceptTerms(user);

    await submit(user);

    expect(
      screen.getByText('Някой артикул не може да се поръча. Върни се в количката и го махни.'),
    ).toBeInTheDocument();
    expect(ordersSent(orders)).toBe(0);
  });

  it('has no accessibility violations, with the form empty and with errors shown', async () => {
    const user = userEvent.setup();
    await openCheckout();

    expect(await findAccessibilityViolations()).toEqual([]);
    await submit(user);
    expect(await findAccessibilityViolations()).toEqual([]);
  });
});
