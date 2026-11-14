import { screen, waitFor, within } from '@testing-library/react';
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
  type Responder,
} from '../../testing/renderApp';

const NUMBER = 12345678;
const PAYMENT_ID = '0b1b6b1e-1f7a-4a93-9a6d-0d3c8c7d9e11';
const REFERENCE = 'test_1900000000_abc';

interface Routes {
  /** The answer to `PUT /api/v1/orders/{number}/payment`. */
  start?: Responder;
  /** The state of the payment, asked for by its identifier. */
  state?: Responder;
  /** The answer to the end of the test payment. */
  complete?: (outcome: unknown) => Response;
  isTestPayment?: boolean;
}

function session(overrides: Record<string, unknown> = {}) {
  return Response.json({
    paymentId: PAYMENT_ID,
    redirectUrl: `/pay/test/${REFERENCE}`,
    expiresAt: '2026-11-13T11:00:00+00:00',
    ...overrides,
  });
}

function stub(routes: Routes = {}) {
  return stubNetwork({
    settings: () => Response.json(settingsFixture()),
    categories: () => Response.json(categoriesFixture()),
    isTestPayment: routes.isTestPayment,
    orders: () => (routes.start ? routes.start() : session()),
    payments: (url, init) => {
      if (url.endsWith('/complete')) {
        const body =
          typeof init?.body === 'string' ? (JSON.parse(init.body) as { outcome: unknown }) : null;
        return routes.complete?.(body?.outcome) ?? new Response(null, { status: 204 });
      }
      return routes.state ? routes.state() : Response.json({ status: 'Pending' });
    },
  });
}

async function openOrder(state: Record<string, unknown> = {}, routes: Routes = {}) {
  const fetchMock = stub(routes);
  renderApp({ pathname: `/order/${String(NUMBER)}`, state: { order: orderFixture(), ...state } });
  const page = await findContent();
  await screen.findByRole('heading', { level: 1, name: 'Поръчката е приета' });
  return { page, fetchMock };
}

function location(): string {
  return screen.getByTestId('location').textContent;
}

describe('paying an order', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('offers to pay, and says until when', async () => {
    const { page } = await openOrder();

    expect(within(page).getByRole('heading', { level: 2, name: 'Плащане' })).toBeInTheDocument();
    expect(within(page).getByRole('button', { name: 'Плати' })).toBeEnabled();
    expect(
      within(page).getByText(/Плати до .*След този час поръчката се отказва/),
    ).toBeInTheDocument();
    expect(within(page).queryByRole('status')).not.toBeInTheDocument();
  });

  it('opens the payment with the protective header and leads to the page of the test provider', async () => {
    const { page, fetchMock } = await openOrder();

    await userEvent.click(within(page).getByRole('button', { name: 'Плати' }));

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Тестово плащане' }),
    ).toBeInTheDocument();
    expect(location()).toBe(`/pay/test/${REFERENCE} PUSH`);
    const call = fetchMock.mock.calls.find(
      ([url]) => url === `/api/v1/orders/${String(NUMBER)}/payment`,
    );
    expect(call?.[1]?.method).toBe('PUT');
    expect(new Headers(call?.[1]?.headers).get('X-Requested-With')).toBe('XMLHttpRequest');
  });

  it('does not follow an address that is another site and is not https', async () => {
    const { page, fetchMock } = await openOrder(
      {},
      { start: () => session({ redirectUrl: '//evil.example/pay' }) },
    );

    await userEvent.click(within(page).getByRole('button', { name: 'Плати' }));

    // The answer has come when the button is free again; give the page a moment to act on it before looking.
    await waitFor(() => {
      expect(
        fetchMock.mock.calls.some(([url]) => url === `/api/v1/orders/${String(NUMBER)}/payment`),
      ).toBe(true);
    });
    await new Promise((resolve) => setTimeout(resolve, 100));
    expect(location()).toBe(`/order/${String(NUMBER)} POP`);
  });

  it.each([
    ['payment.order_not_payable', 409, 'Поръчката вече не може да се плати.'],
    ['payment.disabled', 404, 'Плащането още не е включено.'],
    ['something.else', 500, 'Плащането не започна. Опитай пак след малко.'],
  ])('says what went wrong when the payment does not start (%s)', async (code, status, text) => {
    const { page } = await openOrder({}, { start: () => Response.json({ code }, { status }) });

    await userEvent.click(within(page).getByRole('button', { name: 'Плати' }));

    expect(await within(page).findByRole('alert')).toHaveTextContent(text);
    expect(location()).toBe(`/order/${String(NUMBER)} POP`);
  });

  it('never shows what the server said when the payment does not start', async () => {
    const { page } = await openOrder(
      {},
      { start: () => Response.json({ code: 'x', detail: 'secret detail' }, { status: 500 }) },
    );

    await userEvent.click(within(page).getByRole('button', { name: 'Плати' }));

    await within(page).findByRole('alert');
    expect(within(page).queryByText(/secret detail/)).not.toBeInTheDocument();
  });

  it.each([
    ['Pending', 'Чакаме потвърждение от доставчика на плащането.', 'Продължи към плащането'],
    ['Failed', 'Плащането не мина.', 'Опитай да платиш отново'],
  ])('shows %s, and still lets the buyer pay', async (status, text, button) => {
    const { page } = await openOrder(
      { paymentId: PAYMENT_ID },
      { state: () => Response.json({ status }) },
    );

    expect(await within(page).findByRole('status')).toHaveTextContent(text);
    expect(within(page).getByRole('button', { name: button })).toBeEnabled();
  });

  it.each([
    ['Succeeded', 'Плащането е прието. Благодарим!'],
    ['Expired', 'Срокът за плащане изтече и поръчката е отказана.'],
    ['PaidWithoutOrder', 'Плащането пристигна, след като поръчката вече беше отказана'],
  ])('shows %s, and takes the button and the deadline away', async (status, text) => {
    const { page } = await openOrder(
      { paymentId: PAYMENT_ID },
      { state: () => Response.json({ status }) },
    );

    expect(await within(page).findByRole('status')).toHaveTextContent(text);
    expect(
      within(page).queryByRole('button', { name: /Плати|платиш|Продължи/ }),
    ).not.toBeInTheDocument();
    expect(within(page).queryByText(/Плати до/)).not.toBeInTheDocument();
  });

  it('asks again while the provider has not answered, and changes by itself when it has', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let answers = 0;
    const { page } = await openOrder(
      { paymentId: PAYMENT_ID },
      {
        state: () => {
          answers += 1;
          return Response.json({ status: answers < 3 ? 'Pending' : 'Succeeded' });
        },
      },
    );
    expect(await within(page).findByRole('status')).toHaveTextContent('Чакаме потвърждение');

    await vi.advanceTimersByTimeAsync(2000);
    await vi.advanceTimersByTimeAsync(2000);

    await waitFor(() => {
      expect(within(page).getByRole('status')).toHaveTextContent('Плащането е прието');
    });
    expect(answers).toBe(3);
  });

  it('stops asking after a number of answers, so a lost message does not keep the page busy', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let answers = 0;
    await openOrder(
      { paymentId: PAYMENT_ID },
      {
        state: () => {
          answers += 1;
          return Response.json({ status: 'Pending' });
        },
      },
    );
    await screen.findByText(/Чакаме потвърждение/);

    await vi.advanceTimersByTimeAsync(2000 * 40);

    expect(answers).toBeLessThanOrEqual(16);
  });

  it('has no accessibility violations, with the payment section', async () => {
    await openOrder(
      { paymentId: PAYMENT_ID },
      { state: () => Response.json({ status: 'Failed' }) },
    );
    await screen.findByRole('status');

    expect(await findAccessibilityViolations()).toEqual([]);
  });
});

describe('the page of the test payment', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  async function openTestPage(state: unknown, routes: Routes = {}) {
    const fetchMock = stub(routes);
    renderApp({ pathname: `/pay/test/${REFERENCE}`, state });
    await screen.findByRole('heading', { level: 1, name: 'Тестово плащане' });
    return { page: await findContent(), fetchMock };
  }

  const fromOrder = () => ({ order: orderFixture(), paymentId: PAYMENT_ID });

  it('says it is a test, and has no field for a card', async () => {
    const { page } = await openTestPage(fromOrder());

    expect(within(page).getByText(/Не въвеждаш карта и не се взимат пари/)).toBeInTheDocument();
    expect(within(page).queryByRole('textbox')).not.toBeInTheDocument();
    expect(within(page).queryByRole('spinbutton')).not.toBeInTheDocument();
    expect(within(page).getByRole('button', { name: 'Плати успешно (тест)' })).toBeEnabled();
    expect(within(page).getByRole('button', { name: 'Откажи плащането (тест)' })).toBeEnabled();
  });

  it('is kept out of search results and has its own title', async () => {
    await openTestPage(fromOrder());

    expect(document.head.querySelector('meta[name="robots"]')?.getAttribute('content')).toBe(
      'noindex',
    );
    expect(document.title).toBe('Тестово плащане – Test Shop');
  });

  it.each([
    ['Плати успешно (тест)', 'Paid'],
    ['Откажи плащането (тест)', 'Failed'],
  ])(
    'ends the payment with "%s" and sends only the outcome, then goes back to the order',
    async (label, outcome) => {
      const outcomes: unknown[] = [];
      const { page, fetchMock } = await openTestPage(fromOrder(), {
        complete: (sent) => {
          outcomes.push(sent);
          return new Response(null, { status: 204 });
        },
        state: () => Response.json({ status: outcome === 'Paid' ? 'Succeeded' : 'Failed' }),
      });

      await userEvent.click(within(page).getByRole('button', { name: label }));

      await screen.findByRole('heading', { level: 1, name: 'Поръчката е приета' });
      expect(location()).toBe(`/order/${String(NUMBER)} PUSH`);
      expect(outcomes).toEqual([outcome]);
      const call = fetchMock.mock.calls.find(([url]) => url.endsWith('/complete'));
      expect(call?.[0]).toBe(`/api/v1/payments/test/${REFERENCE}/complete`);
      expect(new Headers(call?.[1]?.headers).get('X-Requested-With')).toBe('XMLHttpRequest');
      expect(await screen.findByRole('status')).toHaveTextContent(
        outcome === 'Paid' ? 'Плащането е прието' : 'Плащането не мина',
      );
    },
  );

  it('says so when the session ended, and stays on the page', async () => {
    const { page } = await openTestPage(fromOrder(), {
      complete: () => Response.json({ code: 'payment.session_expired' }, { status: 422 }),
    });

    await userEvent.click(within(page).getByRole('button', { name: 'Плати успешно (тест)' }));

    expect(await within(page).findByRole('alert')).toHaveTextContent('Сесията за плащане изтече');
    expect(location()).toBe(`/pay/test/${REFERENCE} POP`);
  });

  it('says that it did not work for any other refusal', async () => {
    const { page } = await openTestPage(fromOrder(), {
      complete: () => Response.json({ code: 'x' }, { status: 500 }),
    });

    await userEvent.click(within(page).getByRole('button', { name: 'Плати успешно (тест)' }));

    expect(await within(page).findByRole('alert')).toHaveTextContent('Не се получи. Опитай пак.');
  });

  it('goes to the start when it was opened without an order', async () => {
    const { page } = await openTestPage(undefined);

    await userEvent.click(within(page).getByRole('button', { name: 'Плати успешно (тест)' }));

    await waitFor(() => {
      expect(location()).toBe('/ PUSH');
    });
    expect(screen.queryByRole('link', { name: 'Назад към поръчката' })).not.toBeInTheDocument();
  });

  it('has no accessibility violations', async () => {
    await openTestPage(fromOrder());

    expect(await findAccessibilityViolations()).toEqual([]);
  });
});

describe('the notice of the test payment', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('is on every page while the payment is a test one', async () => {
    stub({ isTestPayment: true });
    renderApp('/');

    const notice = await screen.findByRole('complementary', { name: 'Тестово плащане и доставка' });

    expect(notice).toHaveTextContent('не се взимат пари и нищо не се изпраща');
  });

  it('is not there when the payment is a real one', async () => {
    stub({ isTestPayment: false });
    renderApp('/');
    await findContent();

    expect(
      screen.queryByRole('complementary', { name: 'Тестово плащане и доставка' }),
    ).not.toBeInTheDocument();
  });
});
