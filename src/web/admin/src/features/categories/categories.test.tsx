import type { AdminCategory } from '@storefront/shared';
import { QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../../queryClient';
import { CategoryFormPage } from './CategoryFormPage';
import { CategoryListPage } from './CategoryListPage';

const BASE = '/api/v1/admin/categories';

function category(id: string, overrides: Partial<AdminCategory> = {}): AdminCategory {
  return {
    id,
    parentId: null,
    name: id,
    slug: id,
    description: null,
    sortOrder: 0,
    isActive: true,
    productCount: 0,
    version: 1,
    ...overrides,
  };
}

interface Call {
  method: string;
  url: string;
  body: unknown;
}

/** A small API in memory: it keeps the rows and their versions, and answers 409 for a stale version or a taken slug. */
function fakeApi(initial: AdminCategory[]) {
  const server = { rows: initial.map((row) => ({ ...row })), calls: [] as Call[] };

  const problem = (status: number, code: string) => Response.json({ code }, { status });
  const ordered = () =>
    [...server.rows].sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));

  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET';
    const parsed: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : undefined;
    const body = (parsed ?? {}) as Record<string, unknown> & { version?: number };
    server.calls.push({ method, url, body: parsed });

    if (url === BASE && method === 'GET') {
      return Promise.resolve(Response.json(ordered()));
    }
    if (url === BASE && method === 'POST') {
      if (server.rows.some((row) => row.slug === body.slug)) {
        return Promise.resolve(problem(409, 'category.slug_taken'));
      }
      const created = category(`new-${String(server.rows.length)}`, body);
      server.rows.push(created);
      return Promise.resolve(Response.json(created, { status: 201 }));
    }

    const match = /^\/api\/v1\/admin\/categories\/([^/]+)(?:\/(\w+))?$/.exec(url);
    const row = server.rows.find((item) => item.id === match?.[1]);
    if (!match || !row) {
      return Promise.resolve(problem(404, 'category.not_found'));
    }
    if (body.version !== row.version) {
      return Promise.resolve(problem(409, 'category.version_conflict'));
    }
    const action = match[2];
    if (method === 'PUT') {
      if (server.rows.some((item) => item.id !== row.id && item.slug === body.slug)) {
        return Promise.resolve(problem(409, 'category.slug_taken'));
      }
      Object.assign(row, body, { version: row.version + 1 });
      return Promise.resolve(Response.json(row));
    }
    if (action === 'activate' || action === 'deactivate') {
      row.isActive = action === 'activate';
      row.version += 1;
      return Promise.resolve(Response.json(row));
    }
    if (action === 'move') {
      const siblings = ordered().filter((item) => item.parentId === row.parentId);
      const index = siblings.indexOf(row);
      const target = body.direction === 'Up' ? index - 1 : index + 1;
      const other = target >= 0 ? siblings.at(target) : undefined;
      if (other) {
        siblings[index] = other;
        siblings[target] = row;
      }
      siblings.forEach((item, position) => {
        if (item.sortOrder !== position) {
          item.sortOrder = position;
          item.version += 1;
        }
      });
      return Promise.resolve(Response.json(ordered()));
    }
    return Promise.resolve(problem(404, 'x'));
  });
  vi.stubGlobal('fetch', fetchMock);
  return {
    server,
    writes: () => server.calls.filter((call) => call.method !== 'GET'),
  };
}

function renderAt(path: string) {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="categories" element={<CategoryListPage />} />
          <Route path="categories/new" element={<CategoryFormPage />} />
          <Route path="categories/:id" element={<CategoryFormPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function type(label: string, value: string) {
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
}

const FIGURES = category('figures', { name: 'Фигури', slug: 'figuri', sortOrder: 0, version: 3 });
const DRAGONS = category('dragons', {
  name: 'Дракони',
  slug: 'dragoni',
  parentId: 'figures',
  productCount: 5,
  version: 4,
});
const TOYS = category('toys', { name: 'Играчки', slug: 'igrachki', sortOrder: 1, version: 2 });
const OLD = category('old', {
  name: 'Стари',
  slug: 'stari',
  sortOrder: 2,
  isActive: false,
  version: 6,
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('Category list', () => {
  it('shows the categories as a tree, with the inactive ones marked in words and their products counted', async () => {
    fakeApi([FIGURES, DRAGONS, TOYS, OLD]);
    renderAt('/categories');

    const tree = await screen.findByRole('list', { name: 'Категории' });
    expect(within(tree).getByText('Фигури')).toBeInTheDocument();
    const sub = within(tree).getByRole('list', { name: 'Подкатегории на „Фигури“' });
    expect(within(sub).getByText('Дракони')).toBeInTheDocument();
    expect(within(sub).getByText(/продукти: 5/)).toBeInTheDocument();
    // The inactive row says so with text, not only with a colour.
    const oldRow = screen.getByText('Стари').closest('.category-row');
    expect(oldRow).toHaveTextContent('Неактивна');
    expect(screen.getAllByText('Неактивна')).toHaveLength(1);
  });

  it('gives every button of a row a name that says which category it is for', async () => {
    fakeApi([FIGURES, TOYS, OLD]);
    renderAt('/categories');

    expect(await screen.findByRole('link', { name: 'Редакция на „Фигури“' })).toHaveAttribute(
      'href',
      '/categories/figures',
    );
    expect(screen.getByRole('button', { name: 'Премести „Играчки“ нагоре' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Премести „Играчки“ надолу' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Деактивирай „Играчки“' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Активирай „Стари“' })).toBeInTheDocument();
  });

  it('says so when there are no categories yet', async () => {
    fakeApi([]);
    renderAt('/categories');

    expect(await screen.findByText(/Още няма категории/)).toBeInTheDocument();
  });

  it('says so and offers another try when the list cannot be loaded', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(Response.json({ code: 'x' }, { status: 500 }))),
    );
    renderAt('/categories');

    expect(await screen.findByRole('alert')).toHaveTextContent('не можаха да се заредят');
    expect(screen.getByRole('button', { name: 'Опитай отново' })).toBeInTheDocument();
  });

  describe('order', () => {
    it('moves a category down with the button, and sends the version it saw', async () => {
      const api = fakeApi([FIGURES, TOYS]);
      renderAt('/categories');

      fireEvent.click(await screen.findByRole('button', { name: 'Премести „Фигури“ надолу' }));

      await waitFor(() => {
        const names = screen.getAllByRole('listitem').map((item) => item.textContent);
        expect(names[0]).toContain('Играчки');
      });
      expect(api.writes()).toEqual([
        { method: 'POST', url: `${BASE}/figures/move`, body: { direction: 'Down', version: 3 } },
      ]);
    });

    it('cannot move the first category up or the last one down, and the buttons are real buttons for the keyboard', async () => {
      fakeApi([FIGURES, TOYS]);
      renderAt('/categories');

      expect(
        await screen.findByRole('button', { name: 'Премести „Фигури“ нагоре' }),
      ).toBeDisabled();
      expect(screen.getByRole('button', { name: 'Премести „Играчки“ надолу' })).toBeDisabled();
      expect(screen.getByRole('button', { name: 'Премести „Фигури“ надолу' })).toBeEnabled();
      expect(screen.getByRole('button', { name: 'Премести „Фигури“ надолу' }).tagName).toBe(
        'BUTTON',
      );
    });

    it('moves within the group of the same parent only', async () => {
      const second = category('tiny', { name: 'Малки', parentId: 'figures', sortOrder: 1 });
      fakeApi([FIGURES, { ...DRAGONS, sortOrder: 0 }, second, TOYS]);
      renderAt('/categories');

      // "Дракони" is the first subcategory, so it cannot go up, even though "Играчки" is on the same screen.
      expect(
        await screen.findByRole('button', { name: 'Премести „Дракони“ нагоре' }),
      ).toBeDisabled();
      expect(screen.getByRole('button', { name: 'Премести „Дракони“ надолу' })).toBeEnabled();
      expect(screen.getByRole('button', { name: 'Премести „Малки“ надолу' })).toBeDisabled();
    });

    it('reloads the list and says so when someone else changed the category first', async () => {
      const api = fakeApi([FIGURES, TOYS]);
      renderAt('/categories');
      const down = await screen.findByRole('button', { name: 'Премести „Фигури“ надолу' });
      // Someone else saves "Фигури" after the list was loaded.
      const row = api.server.rows.find((item) => item.id === 'figures');
      if (row) {
        row.version += 1;
      }

      fireEvent.click(down);

      expect(await screen.findByRole('alert')).toHaveTextContent('Някой друг промени категорията');
      await waitFor(() => {
        expect(api.server.calls.filter((call) => call.url === BASE)).toHaveLength(2);
      });
      // With the fresh version the same action works.
      fireEvent.click(screen.getByRole('button', { name: 'Премести „Фигури“ надолу' }));
      await waitFor(() => {
        expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      });
      expect(api.server.rows.find((item) => item.id === 'figures')?.sortOrder).toBe(1);
    });
  });

  describe('deactivating', () => {
    it('asks first, says what happens to the subcategories and products, and sends nothing yet', async () => {
      const api = fakeApi([FIGURES, DRAGONS, TOYS]);
      renderAt('/categories');

      fireEvent.click(await screen.findByRole('button', { name: 'Деактивирай „Фигури“' }));

      const dialog = screen.getByRole('dialog', { name: 'Деактивиране на „Фигури“' });
      expect(dialog).toHaveTextContent('изчезнат от магазина');
      expect(dialog).toHaveTextContent('Подкатегории: 1. Продукти: 5.');
      expect(dialog).toHaveTextContent('Нищо не се изтрива');
      expect(api.writes()).toEqual([]);
    });

    it('does nothing when the administrator cancels, with the button or with Esc', async () => {
      const api = fakeApi([FIGURES, TOYS]);
      renderAt('/categories');

      fireEvent.click(await screen.findByRole('button', { name: 'Деактивирай „Фигури“' }));
      fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Отказ' }));
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

      fireEvent.click(screen.getByRole('button', { name: 'Деактивирай „Фигури“' }));
      fireEvent(screen.getByRole('dialog'), new Event('cancel', { cancelable: true }));
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

      expect(api.writes()).toEqual([]);
      expect(screen.queryByText('Неактивна')).not.toBeInTheDocument();
    });

    it('puts the focus on "Отказ", so a stray Enter does not confirm', async () => {
      fakeApi([FIGURES]);
      renderAt('/categories');

      fireEvent.click(await screen.findByRole('button', { name: 'Деактивирай „Фигури“' }));

      expect(
        within(screen.getByRole('dialog')).getByRole('button', { name: 'Отказ' }),
      ).toHaveFocus();
    });

    it('deactivates after the confirmation, with the version, and the row becomes inactive', async () => {
      const api = fakeApi([FIGURES, TOYS]);
      renderAt('/categories');

      fireEvent.click(await screen.findByRole('button', { name: 'Деактивирай „Фигури“' }));
      fireEvent.click(
        within(screen.getByRole('dialog')).getByRole('button', { name: 'Деактивирай' }),
      );

      expect(await screen.findByText('Неактивна')).toBeInTheDocument();
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      expect(api.writes()).toEqual([
        { method: 'POST', url: `${BASE}/figures/deactivate`, body: { version: 3 } },
      ]);
      expect(screen.getByRole('button', { name: 'Активирай „Фигури“' })).toBeInTheDocument();
    });

    it('closes the dialog and says so when someone else changed the category first', async () => {
      const api = fakeApi([FIGURES, TOYS]);
      renderAt('/categories');
      fireEvent.click(await screen.findByRole('button', { name: 'Деактивирай „Фигури“' }));
      const row = api.server.rows.find((item) => item.id === 'figures');
      if (row) {
        row.version += 1;
      }

      fireEvent.click(
        within(screen.getByRole('dialog')).getByRole('button', { name: 'Деактивирай' }),
      );

      expect(await screen.findByRole('alert')).toHaveTextContent('Някой друг промени категорията');
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      expect(row?.isActive).toBe(true);
    });
  });

  it('activates a category at once, without a question', async () => {
    const api = fakeApi([FIGURES, OLD]);
    renderAt('/categories');

    fireEvent.click(await screen.findByRole('button', { name: 'Активирай „Стари“' }));

    await waitFor(() => {
      expect(screen.queryByText('Неактивна')).not.toBeInTheDocument();
    });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(api.writes()).toEqual([
      { method: 'POST', url: `${BASE}/old/activate`, body: { version: 6 } },
    ]);
  });
});

describe('Category form', () => {
  it('suggests the slug from the name while typing', async () => {
    fakeApi([]);
    renderAt('/categories/new');
    await screen.findByLabelText('Име');

    type('Име', 'Щампи и ножове');

    expect(screen.getByLabelText(/Слъг/)).toHaveValue('shtampi-i-nozhove');
  });

  it('stops following the name once the administrator edits the slug', async () => {
    fakeApi([]);
    renderAt('/categories/new');
    await screen.findByLabelText('Име');

    type('Име', 'Фигури');
    type('Слъг (част от адреса)', 'moi-slug');
    type('Име', 'Фигури и играчки');

    expect(screen.getByLabelText(/Слъг/)).toHaveValue('moi-slug');
  });

  it('creates a category at the end of the list and goes back to the list', async () => {
    const api = fakeApi([FIGURES, TOYS]);
    renderAt('/categories/new');
    await screen.findByLabelText('Име');

    type('Име', 'Кутии');
    type('Описание', 'Кутии за подаръци');
    fireEvent.click(screen.getByRole('button', { name: 'Запази' }));

    expect(await screen.findByRole('list', { name: 'Категории' })).toBeInTheDocument();
    expect(api.writes()).toEqual([
      {
        method: 'POST',
        url: BASE,
        body: {
          name: 'Кутии',
          slug: 'kutii',
          parentId: null,
          description: 'Кутии за подаръци',
          sortOrder: 2,
          isActive: true,
        },
      },
    ]);
  });

  it('shows what is wrong next to each field, sends nothing and moves the focus to the first one', async () => {
    const api = fakeApi([]);
    renderAt('/categories/new');
    await screen.findByLabelText('Име');
    type('Слъг (част от адреса)', 'Лош слъг');
    type('Подредба (число)', 'x');

    fireEvent.click(screen.getByRole('button', { name: 'Запази' }));

    const name = screen.getByLabelText('Име');
    expect(name).toBeInvalid();
    expect(name).toHaveAccessibleDescription('Въведи име.');
    expect(name).toHaveFocus();
    expect(screen.getByLabelText(/Слъг/)).toHaveAccessibleDescription(/само малки латински букви/);
    expect(screen.getByLabelText('Подредба (число)')).toBeInvalid();
    expect(api.writes()).toEqual([]);
  });

  it('shows "слъгът е зает" next to the slug field and keeps what was typed', async () => {
    const api = fakeApi([FIGURES]);
    renderAt('/categories/new');
    await screen.findByLabelText('Име');
    type('Име', 'Фигури');

    fireEvent.click(screen.getByRole('button', { name: 'Запази' }));

    await waitFor(() => {
      expect(screen.getByLabelText(/Слъг/)).toHaveAccessibleDescription(/Слъгът е зает/);
    });
    expect(screen.getByLabelText(/Слъг/)).toBeInvalid();
    expect(screen.getByLabelText(/Слъг/)).toHaveFocus();
    expect(screen.getByLabelText('Име')).toHaveValue('Фигури');
    expect(api.writes()).toHaveLength(1);
  });

  it('offers only top-level categories as a parent', async () => {
    fakeApi([FIGURES, DRAGONS, TOYS]);
    renderAt('/categories/new');

    const select = await screen.findByLabelText('Родителска категория');

    const options = within(select)
      .getAllByRole('option')
      .map((option) => option.textContent);
    expect(options).toEqual(['Няма (главна категория)', 'Фигури', 'Играчки']);
  });

  describe('editing', () => {
    it('loads the category, warns about changing the slug, and does not follow the name', async () => {
      fakeApi([FIGURES, TOYS]);
      renderAt('/categories/figures');

      expect(await screen.findByLabelText('Име')).toHaveValue('Фигури');
      expect(screen.getByLabelText(/Слъг/)).toHaveValue('figuri');
      expect(screen.getByLabelText(/Слъг/)).toHaveAccessibleDescription(/старите адреси/);
      type('Име', 'Нови фигури');
      expect(screen.getByLabelText(/Слъг/)).toHaveValue('figuri');
    });

    it('does not offer the category itself as its parent', async () => {
      fakeApi([FIGURES, TOYS]);
      renderAt('/categories/toys');

      const select = await screen.findByLabelText('Родителска категория');

      const options = within(select)
        .getAllByRole('option')
        .map((option) => option.textContent);
      expect(options).toEqual(['Няма (главна категория)', 'Фигури']);
    });

    it('does not let a category with subcategories choose a parent, and says why', async () => {
      fakeApi([FIGURES, DRAGONS, TOYS]);
      renderAt('/categories/figures');

      const select = await screen.findByLabelText('Родителска категория');

      expect(select).toBeDisabled();
      expect(select).toHaveAccessibleDescription(/не може да стане подкатегория/);
    });

    it('saves with the version the form was loaded with and goes back to the list', async () => {
      const api = fakeApi([FIGURES, TOYS]);
      renderAt('/categories/figures');
      await screen.findByLabelText('Име');

      type('Име', 'Нови фигури');
      fireEvent.click(screen.getByRole('button', { name: 'Запази' }));

      expect(await screen.findByRole('list', { name: 'Категории' })).toBeInTheDocument();
      expect(api.writes()).toEqual([
        {
          method: 'PUT',
          url: `${BASE}/figures`,
          body: {
            name: 'Нови фигури',
            slug: 'figuri',
            parentId: null,
            description: null,
            sortOrder: 0,
            isActive: true,
            version: 3,
          },
        },
      ]);
    });

    it('does not offer to switch the category off here, so the question before deactivating cannot be skipped', async () => {
      fakeApi([FIGURES]);
      renderAt('/categories/figures');
      await screen.findByLabelText('Име');

      expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
      expect(screen.getByText(/Деактивира се от списъка/)).toBeInTheDocument();
    });

    it('tells about a change by someone else, and "Презареди" loads what the server has now', async () => {
      const api = fakeApi([FIGURES, TOYS]);
      renderAt('/categories/figures');
      await screen.findByLabelText('Име');
      const row = api.server.rows.find((item) => item.id === 'figures');
      if (row) {
        row.name = 'Фигурки от друг';
        row.version += 1;
      }

      type('Име', 'Моето име');
      fireEvent.click(screen.getByRole('button', { name: 'Запази' }));

      const alert = await screen.findByRole('alert');
      expect(alert).toHaveTextContent('Някой друг промени тази категория');
      expect(screen.getByLabelText('Име')).toHaveValue('Моето име');

      fireEvent.click(within(alert).getByRole('button', { name: 'Презареди категорията' }));

      await waitFor(() => {
        expect(screen.getByLabelText('Име')).toHaveValue('Фигурки от друг');
      });
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      expect(row?.name).toBe('Фигурки от друг');
    });

    it('says so when the category does not exist', async () => {
      fakeApi([FIGURES]);
      renderAt('/categories/nothing');

      expect(await screen.findByRole('alert')).toHaveTextContent('Такава категория няма');
      expect(screen.getByRole('link', { name: 'Към списъка с категории' })).toBeInTheDocument();
    });
  });
});
