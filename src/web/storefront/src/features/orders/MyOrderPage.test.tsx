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

type OrdersResponder = (url: string, init?: RequestInit) => Response | Promise<Response>;

function stub(orders: OrdersResponder) {
  return stubNetwork({
    settings: () => Response.json(settingsFixture()),
    categories: () => Response.json(categoriesFixture()),
    orders,
  });
}

function robots(): string | null {
  return document.head.querySelector('meta[name="robots"]')?.getAttribute('content') ?? null;
}

function bodyOf(init: RequestInit | undefined): unknown {
  return typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined;
}

async function fill(user: ReturnType<typeof userEvent.setup>, number: string, email: string) {
  if (number !== '') {
    await user.type(screen.getByLabelText('Номер на поръчката'), number);
  }
  if (email !== '') {
    await user.type(screen.getByLabelText('Имейл'), email);
  }
  await user.click(screen.getByRole('button', { name: 'Покажи поръчката' }));
}

describe('page "My order"', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the form, is kept out of search results and has its own title', async () => {
    stub(() => Response.json(orderFixture()));
    renderApp('/my-order');

    expect(await screen.findByRole('heading', { level: 1, name: 'Поръчката ми' })).toBeVisible();
    expect(screen.getByLabelText('Номер на поръчката')).toBeVisible();
    expect(screen.getByLabelText('Имейл')).toBeVisible();
    expect(robots()).toBe('noindex');
    expect(document.title).toBe('Поръчката ми – Test Shop');
  });

  it('finds the order by the number and the email, and shows its status, steps and lines', async () => {
    const fetchMock = stub(() => Response.json(orderFixture({ status: 'InProduction' })));
    const user = userEvent.setup();
    renderApp('/my-order');
    await screen.findByRole('heading', { level: 1, name: 'Поръчката ми' });

    await fill(user, 'PR-12345678', 'buyer@example.test');

    const page = await findContent();
    expect(await within(page).findByText('PR-12345678')).toBeVisible();
    expect(within(page).getByText('В производство', { selector: 'strong' })).toBeVisible();
    const steps = within(page).getByRole('list', { name: 'Стъпки на поръчката' });
    expect(within(steps).getAllByRole('listitem')).toHaveLength(6);
    expect(within(steps).getByText('В производство')).toHaveAttribute('aria-current', 'step');
    expect(within(steps).getByText('Платена')).not.toHaveAttribute('aria-current');
    expect(within(page).getByText('Dragon')).toBeVisible();
    const call = fetchMock.mock.calls.find(([url]) => url === '/api/v1/orders/lookup');
    expect(bodyOf(call?.[1])).toEqual({ number: 12345678, email: 'buyer@example.test' });
    expect(call?.[0]).not.toContain('buyer');
  });

  it('shows no steps for a cancelled order, only the word', async () => {
    stub(() => Response.json(orderFixture({ status: 'Cancelled' })));
    const user = userEvent.setup();
    renderApp('/my-order');
    await screen.findByRole('heading', { level: 1, name: 'Поръчката ми' });

    await fill(user, '12345678', 'buyer@example.test');

    const page = await findContent();
    expect(await within(page).findByText('Отказана')).toBeVisible();
    expect(
      within(page).queryByRole('list', { name: 'Стъпки на поръчката' }),
    ).not.toBeInTheDocument();
  });

  it('shows a neutral text, not the raw word, for a status the page does not know', async () => {
    stub(() => Response.json(orderFixture({ status: 'Teleported' })));
    const user = userEvent.setup();
    renderApp('/my-order');
    await screen.findByRole('heading', { level: 1, name: 'Поръчката ми' });

    await fill(user, '12345678', 'buyer@example.test');

    const page = await findContent();
    expect(await within(page).findByText('Непознато състояние')).toBeVisible();
    expect(within(page).queryByText('Teleported')).not.toBeInTheDocument();
  });

  it('says the same thing for a wrong number and a wrong email, and keeps what was typed', async () => {
    stub(() => Response.json({ code: 'order.not_found' }, { status: 404 }));
    const user = userEvent.setup();
    renderApp('/my-order');
    await screen.findByRole('heading', { level: 1, name: 'Поръчката ми' });

    await fill(user, '12345678', 'wrong@example.test');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Не намерихме поръчка с тези данни. Провери номера и имейла.',
    );
    expect(screen.getByLabelText('Имейл')).toHaveValue('wrong@example.test');
  });

  it('asks to wait after too many requests', async () => {
    stub(() => new Response(null, { status: 429 }));
    const user = userEvent.setup();
    renderApp('/my-order');
    await screen.findByRole('heading', { level: 1, name: 'Поръчката ми' });

    await fill(user, '12345678', 'buyer@example.test');

    expect(await screen.findByRole('alert')).toHaveTextContent('Твърде много опити');
  });

  it('marks a number that is not eight digits and an email that is not one, without calling the server', async () => {
    const orders = vi.fn<OrdersResponder>(() => Response.json(orderFixture()));
    stub(orders);
    const user = userEvent.setup();
    renderApp('/my-order');
    await screen.findByRole('heading', { level: 1, name: 'Поръчката ми' });

    await fill(user, '123', 'not-an-email');

    expect(screen.getByLabelText('Номер на поръчката')).toHaveAccessibleDescription(
      /Номерът е осем цифри/,
    );
    expect(screen.getByLabelText('Номер на поръчката')).toBeInvalid();
    expect(screen.getByLabelText('Имейл')).toBeInvalid();
    expect(screen.getByLabelText('Номер на поръчката')).toHaveFocus();
    expect(orders).not.toHaveBeenCalled();
  });

  it('goes back to the form to look at another order', async () => {
    stub(() => Response.json(orderFixture()));
    const user = userEvent.setup();
    renderApp('/my-order');
    await screen.findByRole('heading', { level: 1, name: 'Поръчката ми' });
    await fill(user, '12345678', 'buyer@example.test');
    await screen.findByText('PR-12345678');

    await user.click(screen.getByRole('button', { name: 'Покажи друга поръчка' }));

    expect(screen.getByLabelText('Номер на поръчката')).toHaveValue('');
  });

  it('opens the order straight from the link of the email, with the key in the body and not in the address', async () => {
    const fetchMock = stub(() => Response.json(orderFixture({ status: 'Shipped' })));
    renderApp('/my-order?token=abc-DEF_123');

    const page = await findContent();
    expect(await within(page).findByText('PR-12345678')).toBeVisible();
    expect(within(page).getByText('Изпратена', { selector: 'strong' })).toBeVisible();
    expect(within(page).queryByLabelText('Номер на поръчката')).not.toBeInTheDocument();
    const call = fetchMock.mock.calls.find(([url]) => url === '/api/v1/orders/lookup-by-token');
    expect(bodyOf(call?.[1])).toEqual({ token: 'abc-DEF_123' });
    expect(call?.[0]).not.toContain('abc-DEF_123');
    expect(robots()).toBe('noindex');
  });

  it('sends the buyer to the form when the link is not valid', async () => {
    stub(() => Response.json({ code: 'order.not_found' }, { status: 404 }));
    renderApp('/my-order?token=wrong');

    expect(await screen.findByRole('alert')).toHaveTextContent('Връзката не е валидна');
    expect(within(await findContent()).getByRole('link', { name: 'Поръчката ми' })).toHaveAttribute(
      'href',
      '/my-order',
    );
  });

  it('has no accessibility violations, in the form and with an order', async () => {
    stub(() => Response.json(orderFixture({ status: 'Paid' })));
    const user = userEvent.setup();
    renderApp('/my-order');
    await findContent();
    await screen.findByLabelText('Номер на поръчката');
    expect(await findAccessibilityViolations()).toEqual([]);

    await fill(user, '12345678', 'buyer@example.test');
    await screen.findByText('PR-12345678');
    expect(await findAccessibilityViolations()).toEqual([]);
  });
});

describe('links to the page "My order"', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('is in the footer of every page', async () => {
    stub(() => Response.json(orderFixture()));
    renderApp('/');

    await findContent();

    expect(await screen.findByRole('link', { name: 'Поръчката ми' })).toHaveAttribute(
      'href',
      '/my-order',
    );
  });

  it('is on the confirmation of an order', async () => {
    stub(() => Response.json(orderFixture()));
    renderApp({ pathname: '/order/12345678', state: { order: orderFixture() } });

    await findContent();

    const links = await screen.findAllByRole('link', { name: /Следи поръчката си/ });
    expect(links[0]).toHaveAttribute('href', '/my-order');
  });
});
