import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../../queryClient';
import { SettingsPage } from './SettingsPage';

const URL = '/api/v1/admin/store-settings';

interface Settings {
  storeName: string;
  defaultCurrency: string;
  defaultLocale: string;
  contactEmail: string;
  contactPhone: string;
  logoKey: string | null;
  primaryColor: string;
  isDemo: boolean;
  version: number;
}

const START: Settings = {
  storeName: 'Принтко 3Д',
  defaultCurrency: 'EUR',
  defaultLocale: 'bg',
  contactEmail: 'shop@example.com',
  contactPhone: '+359 88 123 456',
  logoKey: null,
  primaryColor: '#2f5d8a',
  isDemo: true,
  version: 5,
};

interface Call {
  method: string;
  url: string;
  body: Record<string, unknown> | undefined;
}

/** A small API in memory: it keeps the row and its version and answers 409 for an old version. */
function fakeApi(options: { reject?: Response } = {}) {
  const server = { row: { ...START }, calls: [] as Call[] };
  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET';
    const body =
      typeof init?.body === 'string'
        ? (JSON.parse(init.body) as Record<string, unknown>)
        : undefined;
    server.calls.push({ method, url, body });
    if (url !== URL) {
      return Promise.resolve(Response.json({}, { status: 404 }));
    }
    if (method === 'GET') {
      return Promise.resolve(Response.json(server.row));
    }
    if (options.reject) {
      return Promise.resolve(options.reject.clone());
    }
    if (body?.version !== server.row.version) {
      return Promise.resolve(
        Response.json({ code: 'store_settings.version_conflict' }, { status: 409 }),
      );
    }
    server.row = {
      ...server.row,
      ...body,
      storeName: String(body.storeName).trim(),
      primaryColor: String(body.primaryColor).toLowerCase(),
      version: server.row.version + 1,
    };
    return Promise.resolve(Response.json(server.row));
  });
  vi.stubGlobal('fetch', fetchMock);
  return { server, writes: () => server.calls.filter((call) => call.method === 'PUT') };
}

function renderPage(client: QueryClient = createQueryClient()) {
  render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <SettingsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return client;
}

async function loaded() {
  await screen.findByLabelText('Име на магазина');
}

function type(label: string, value: string) {
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
}

function submit() {
  fireEvent.click(screen.getByRole('button', { name: 'Запази' }));
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('settings screen', () => {
  it('loads the form with the current values and labelled fields', async () => {
    fakeApi();
    renderPage();

    expect(screen.getByRole('status')).toHaveTextContent('Зареждане на настройките');
    await loaded();

    expect(screen.getByLabelText('Име на магазина')).toHaveValue('Принтко 3Д');
    expect(screen.getByLabelText('Валута')).toHaveValue('EUR');
    expect(screen.getByLabelText('Език')).toHaveValue('bg');
    expect(screen.getByLabelText('Имейл за контакт')).toHaveValue('shop@example.com');
    expect(screen.getByLabelText('Телефон за контакт')).toHaveValue('+359 88 123 456');
    expect(screen.getByLabelText('Основен цвят')).toHaveValue('#2f5d8a');
    expect(screen.getByLabelText('Избор на цвят')).toHaveValue('#2f5d8a');
    expect(screen.getByRole('checkbox', { name: /лентата „демо версия“/ })).toBeChecked();
    expect(screen.getByText(/след презареждане на страницата му/)).toBeInTheDocument();
  });

  it('says so when the settings cannot be loaded, and offers to try again', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(Response.json({}, { status: 500 }))),
    );
    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent('Настройките не се заредиха');
    expect(screen.getByRole('button', { name: 'Опитай отново' })).toBeInTheDocument();
  });

  it.each([
    ['Име на магазина', '   ', 'Въведи име на магазина.'],
    ['Имейл за контакт', 'shop@example', 'Имейлът не е във вярен формат'],
    ['Телефон за контакт', 'call me', 'Телефонът може да съдържа само цифри'],
    ['Основен цвят', '#12', 'Цветът трябва да е във формат #RRGGBB.'],
  ])('shows the error of "%s" next to the field and sends nothing', async (label, value, text) => {
    const { writes } = fakeApi();
    renderPage();
    await loaded();

    type(label, value);
    submit();

    const field = screen.getByLabelText(label);
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(field).toHaveAccessibleDescription(expect.stringContaining(text));
    expect(field).toHaveFocus();
    expect(writes()).toHaveLength(0);
  });

  it('refuses a colour that is too light, with an explanation, and does not rely on colour alone', async () => {
    const { writes } = fakeApi();
    renderPage();
    await loaded();

    type('Основен цвят', '#ffffff');

    expect(screen.getByLabelText('Основен цвят')).toHaveAccessibleDescription(
      expect.stringContaining('недостатъчен'),
    );
    submit();
    expect(screen.getByLabelText('Основен цвят')).toHaveAccessibleDescription(
      expect.stringContaining('контрастът му с бялото трябва да е най-малко 3:1'),
    );
    expect(writes()).toHaveLength(0);
  });

  it('shows the contrast and a preview for the chosen colour, from the picker too', async () => {
    fakeApi();
    renderPage();
    await loaded();

    fireEvent.change(screen.getByLabelText('Избор на цвят'), { target: { value: '#000000' } });

    expect(screen.getByLabelText('Основен цвят')).toHaveValue('#000000');
    expect(screen.getByLabelText('Основен цвят')).toHaveAccessibleDescription(
      expect.stringContaining('21,0:1 – достатъчен'),
    );
    expect(screen.getByText('Бутон')).toHaveStyle({ backgroundColor: '#000000', color: '#ffffff' });
  });

  it('saves with the version, trims and lowercases, and announces "Запазено"', async () => {
    const { writes, server } = fakeApi();
    renderPage();
    await loaded();

    type('Име на магазина', '  Нов магазин  ');
    type('Основен цвят', '#2F5D8A');
    type('Телефон за контакт', '');
    fireEvent.click(screen.getByRole('checkbox', { name: /лентата „демо версия“/ }));
    submit();
    // Switching the demo bar off needs a confirmation.
    fireEvent.click(
      within(screen.getByRole('dialog')).getByRole('button', { name: 'Изключи лентата' }),
    );

    await waitFor(() => {
      expect(screen.getByRole('status')).toHaveTextContent('Запазено');
    });
    expect(writes()).toHaveLength(1);
    expect(writes()[0]?.body).toEqual({
      storeName: 'Нов магазин',
      defaultCurrency: 'EUR',
      defaultLocale: 'bg',
      contactEmail: 'shop@example.com',
      contactPhone: '',
      primaryColor: '#2f5d8a',
      isDemo: false,
      version: 5,
    });
    expect(server.row.version).toBe(6);
    expect(screen.getByLabelText('Име на магазина')).toHaveValue('Нов магазин');
  });

  it('uses the new version for the next save, and the status goes away when the form is edited', async () => {
    const { writes } = fakeApi();
    renderPage();
    await loaded();

    type('Име на магазина', 'Първо');
    submit();
    await waitFor(() => {
      expect(screen.getByRole('status')).toHaveTextContent('Запазено');
    });
    type('Име на магазина', 'Второ');
    expect(screen.getByRole('status')).toBeEmptyDOMElement();
    submit();

    await waitFor(() => {
      expect(writes()).toHaveLength(2);
    });
    expect(writes()[1]?.body).toMatchObject({ storeName: 'Второ', version: 6 });
  });

  it('refreshes the currency and language of the product forms after a currency change', async () => {
    fakeApi();
    const client = renderPage();
    client.setQueryData(['admin', 'store-money'], { currency: 'EUR', locale: 'bg' });
    await loaded();

    type('Валута', 'BGN');
    submit();
    fireEvent.click(
      within(screen.getByRole('dialog')).getByRole('button', { name: 'Смени валутата' }),
    );

    await waitFor(() => {
      expect(client.getQueryData(['admin', 'store-money'])).toEqual({
        currency: 'BGN',
        locale: 'bg',
      });
    });
  });

  describe('confirmations', () => {
    it('asks before the currency changes, and sends nothing when the answer is no', async () => {
      const { writes } = fakeApi();
      renderPage();
      await loaded();

      type('Валута', 'BGN');
      submit();

      const dialog = screen.getByRole('dialog', { name: 'Смяна на валутата' });
      expect(dialog).toHaveTextContent('няма да бъдат преизчислени');
      fireEvent.click(within(dialog).getByRole('button', { name: 'Отказ' }));
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      expect(writes()).toHaveLength(0);
    });

    it('does not ask when the currency stays the same', async () => {
      const { writes } = fakeApi();
      renderPage();
      await loaded();

      type('Име на магазина', 'Друго име');
      submit();

      await waitFor(() => {
        expect(writes()).toHaveLength(1);
      });
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('asks before the demo bar is switched off, and sends nothing when the answer is no', async () => {
      const { writes } = fakeApi();
      renderPage();
      await loaded();

      fireEvent.click(screen.getByRole('checkbox', { name: /лентата „демо версия“/ }));
      submit();

      const dialog = screen.getByRole('dialog', { name: 'Изключване на демо версията' });
      fireEvent.click(within(dialog).getByRole('button', { name: 'Отказ' }));
      expect(writes()).toHaveLength(0);
    });

    it('does not ask when the demo bar is switched on', async () => {
      const { writes, server } = fakeApi();
      server.row.isDemo = false;
      renderPage();
      await loaded();

      fireEvent.click(screen.getByRole('checkbox', { name: /лентата „демо версия“/ }));
      submit();

      await waitFor(() => {
        expect(writes()).toHaveLength(1);
      });
      expect(writes()[0]?.body).toMatchObject({ isDemo: true });
    });

    it('asks both questions one after the other and sends once', async () => {
      const { writes } = fakeApi();
      renderPage();
      await loaded();

      type('Валута', 'BGN');
      fireEvent.click(screen.getByRole('checkbox', { name: /лентата „демо версия“/ }));
      submit();
      fireEvent.click(
        within(screen.getByRole('dialog', { name: 'Смяна на валутата' })).getByRole('button', {
          name: 'Смени валутата',
        }),
      );
      expect(writes()).toHaveLength(0);
      fireEvent.click(
        within(screen.getByRole('dialog', { name: 'Изключване на демо версията' })).getByRole(
          'button',
          { name: 'Изключи лентата' },
        ),
      );

      await waitFor(() => {
        expect(writes()).toHaveLength(1);
      });
    });
  });

  describe('errors of the API', () => {
    it('shows the version conflict with a reload button that brings the new values', async () => {
      const { server, writes } = fakeApi();
      renderPage();
      await loaded();
      // Someone else saves first.
      server.row = { ...server.row, storeName: 'От друг админ', version: 9 };

      type('Име на магазина', 'Моето име');
      submit();

      const alert = await screen.findByRole('alert');
      expect(alert).toHaveTextContent('Някой друг промени настройките');
      fireEvent.click(within(alert).getByRole('button', { name: 'Презареди' }));

      await waitFor(() => {
        expect(screen.getByLabelText('Име на магазина')).toHaveValue('От друг админ');
      });
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      type('Име на магазина', 'Моето име');
      submit();
      await waitFor(() => {
        expect(writes()).toHaveLength(2);
      });
      expect(writes()[1]?.body).toMatchObject({ version: 9 });
    });

    it('shows a locked currency next to the currency field', async () => {
      fakeApi({
        reject: Response.json({ code: 'store_settings.currency_locked' }, { status: 409 }),
      });
      renderPage();
      await loaded();

      type('Валута', 'BGN');
      submit();
      fireEvent.click(
        within(screen.getByRole('dialog')).getByRole('button', { name: 'Смени валутата' }),
      );

      await waitFor(() => {
        expect(screen.getByLabelText('Валута')).toHaveAttribute('aria-invalid', 'true');
      });
      expect(screen.getByLabelText('Валута')).toHaveAccessibleDescription(
        expect.stringContaining('Валутата не може да се смени'),
      );
      expect(screen.getByLabelText('Валута')).toHaveFocus();
    });

    it('shows the fields the API rejected next to them', async () => {
      fakeApi({
        reject: Response.json(
          { code: 'validation.failed', errors: { contactEmail: ['x'], primaryColor: ['y'] } },
          { status: 400 },
        ),
      });
      renderPage();
      await loaded();

      submit();

      await waitFor(() => {
        expect(screen.getByLabelText('Имейл за контакт')).toHaveAttribute('aria-invalid', 'true');
      });
      expect(screen.getByLabelText('Основен цвят')).toHaveAttribute('aria-invalid', 'true');
      expect(screen.getByLabelText('Имейл за контакт')).toHaveFocus();
      expect(screen.getByLabelText('Имейл за контакт')).toHaveAccessibleDescription(
        'Сървърът не прие имейла.',
      );
    });

    it('says the save failed for any other answer, and keeps what was typed', async () => {
      fakeApi({ reject: Response.json({}, { status: 500 }) });
      renderPage();
      await loaded();

      type('Име на магазина', 'Ново име');
      submit();

      expect(await screen.findByRole('alert')).toHaveTextContent('Настройките не се запазиха');
      expect(screen.getByLabelText('Име на магазина')).toHaveValue('Ново име');
    });
  });
});
