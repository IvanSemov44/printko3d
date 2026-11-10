import type { AdminCategory, AdminProduct, AdminVariant } from '@storefront/shared';
import { QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../../queryClient';
import { ProductFormPage } from './ProductFormPage';
import { ProductListPage } from './ProductListPage';

const PRODUCTS = '/api/v1/admin/products';
const CATEGORIES = '/api/v1/admin/categories';
const SETTINGS = '/api/v1/store-settings';

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

function variant(id: string, overrides: Partial<AdminVariant> = {}): AdminVariant {
  return {
    id,
    sku: id.toUpperCase(),
    name: id,
    price: 1500,
    compareAtPrice: null,
    stockQuantity: 5,
    weightGrams: 100,
    attributes: {},
    isActive: true,
    sortOrder: 0,
    ...overrides,
  };
}

function product(id: string, overrides: Partial<AdminProduct> = {}): AdminProduct {
  return {
    id,
    categoryId: 'figures',
    name: id,
    slug: id,
    shortDescription: 'Кратко',
    description: 'Дълго',
    fulfilmentType: 'InStock',
    leadTimeDays: null,
    status: 'Draft',
    seoTitle: null,
    seoDescription: null,
    version: 1,
    variants: [],
    ...overrides,
  };
}

interface Call {
  method: string;
  url: string;
  body: unknown;
}

/** A small API in memory: it keeps the products and their versions and answers with the codes of the real one. */
function fakeApi(initialProducts: AdminProduct[], initialCategories: AdminCategory[]) {
  const server = {
    products: initialProducts.map((item) => structuredClone(item)),
    categories: initialCategories,
    calls: [] as Call[],
  };
  const problem = (status: number, code: string) => Response.json({ code }, { status });
  const byName = () => [...server.products].sort((a, b) => a.name.localeCompare(b.name));

  const listItem = (item: AdminProduct) => {
    const active = item.variants.filter((v) => v.isActive);
    return {
      id: item.id,
      name: item.name,
      slug: item.slug,
      categoryId: item.categoryId,
      categoryName: server.categories.find((c) => c.id === item.categoryId)?.name ?? '',
      status: item.status,
      fulfilmentType: item.fulfilmentType,
      variantCount: item.variants.length,
      minPrice: active.length === 0 ? null : Math.min(...active.map((v) => v.price)),
      totalStock:
        item.fulfilmentType === 'InStock'
          ? active.reduce((sum, v) => sum + (v.stockQuantity ?? 0), 0)
          : null,
      version: item.version,
    };
  };

  const fetchMock = vi.fn((rawUrl: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET';
    const [url = '', query = ''] = rawUrl.split('?');
    const params = new URLSearchParams(query);
    const parsed: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : undefined;
    const body = (parsed ?? {}) as Record<string, unknown> & { version?: number };
    server.calls.push({ method, url: rawUrl, body: parsed });
    const json = (value: unknown, status = 200) =>
      Promise.resolve(Response.json(value, { status }));
    const fail = (status: number, code: string) => Promise.resolve(problem(status, code));

    if (url === SETTINGS) {
      return json({ defaultCurrency: 'EUR', defaultLocale: 'bg' });
    }
    if (url === CATEGORIES) {
      return json(server.categories);
    }
    if (url === PRODUCTS && method === 'GET') {
      let rows = byName();
      const status = params.get('status');
      const categoryId = params.get('categoryId');
      const search = params.get('search');
      if (status) {
        rows = rows.filter((item) => item.status === status);
      }
      if (categoryId) {
        rows = rows.filter((item) => item.categoryId === categoryId);
      }
      if (search) {
        rows = rows.filter((item) => item.name.toLowerCase().includes(search.toLowerCase()));
      }
      const page = Number(params.get('page'));
      const pageSize = Number(params.get('pageSize'));
      return json({
        items: rows.slice((page - 1) * pageSize, page * pageSize).map(listItem),
        page,
        pageSize,
        totalCount: rows.length,
        totalPages: Math.ceil(rows.length / pageSize),
      });
    }
    if (url === PRODUCTS && method === 'POST') {
      if (server.products.some((item) => item.slug === body.slug)) {
        return fail(409, 'product.slug_taken');
      }
      const created = product(`new-${String(server.products.length)}`, body);
      server.products.push(created);
      return json(created, 201);
    }

    const match =
      /^\/api\/v1\/admin\/products\/([^/]+)(?:\/(status|variants)(?:\/([^/]+)(?:\/(active|move))?)?)?$/.exec(
        url,
      );
    const item = server.products.find((candidate) => candidate.id === match?.[1]);
    if (!match || !item) {
      return fail(404, 'product.not_found');
    }
    const part = match.at(2);
    const variantId = match.at(3);
    const action = match.at(4);

    if (method === 'GET' && part === undefined) {
      return json(item);
    }
    if (part === undefined && method === 'PUT') {
      if (body.version !== item.version) {
        return fail(409, 'product.version_conflict');
      }
      if (server.products.some((other) => other.id !== item.id && other.slug === body.slug)) {
        return fail(409, 'product.slug_taken');
      }
      const stock = (body.variantStock ?? {}) as Record<string, number>;
      if (body.fulfilmentType === 'InStock' && item.fulfilmentType === 'MadeToOrder') {
        if (item.variants.some((v) => !(v.id in stock))) {
          return fail(400, 'validation.failed');
        }
        item.variants.forEach((v) => {
          v.stockQuantity = stock[v.id] ?? 0;
        });
      }
      if (body.fulfilmentType === 'MadeToOrder' && item.fulfilmentType === 'InStock') {
        item.variants.forEach((v) => {
          v.stockQuantity = null;
        });
      }
      const fields = { ...body };
      delete fields.version;
      delete fields.variantStock;
      Object.assign(item, fields, { version: item.version + 1 });
      return json(item);
    }
    if (part === 'status') {
      if (body.version !== item.version) {
        return fail(409, 'product.version_conflict');
      }
      if (body.status === 'Published' && !item.variants.some((v) => v.isActive)) {
        return fail(409, 'product.cannot_publish_without_active_variant');
      }
      item.status = body.status as AdminProduct['status'];
      item.version += 1;
      return json(item);
    }
    if (part === 'variants' && variantId === undefined && method === 'POST') {
      const taken = server.products.some((other) => other.variants.some((v) => v.sku === body.sku));
      if (taken) {
        return fail(409, 'variant.sku_taken');
      }
      item.variants.push(
        variant(`v-${String(item.variants.length + 1)}`, body as Partial<AdminVariant>),
      );
      return json(item, 201);
    }
    const target = item.variants.find((v) => v.id === variantId);
    if (!target) {
      return fail(404, 'variant.not_found');
    }
    if (action === 'active') {
      const isActive = body.isActive as boolean;
      if (
        !isActive &&
        item.status === 'Published' &&
        item.variants.filter((v) => v.isActive).length === 1
      ) {
        return fail(409, 'variant.last_active');
      }
      target.isActive = isActive;
      return json(item);
    }
    if (action === 'move') {
      const ordered = [...item.variants].sort((a, b) => a.sortOrder - b.sortOrder);
      const index = ordered.indexOf(target);
      const targetIndex = body.direction === 'Up' ? index - 1 : index + 1;
      const other = targetIndex >= 0 ? ordered.at(targetIndex) : undefined;
      if (other) {
        ordered[index] = other;
        ordered[targetIndex] = target;
      }
      ordered.forEach((v, position) => {
        v.sortOrder = position;
      });
      item.variants = ordered;
      return json(item);
    }
    if (method === 'PUT') {
      if (
        server.products.some((other) =>
          other.variants.some((v) => v.id !== target.id && v.sku === body.sku),
        )
      ) {
        return fail(409, 'variant.sku_taken');
      }
      Object.assign(target, body);
      return json(item);
    }
    return fail(404, 'x');
  });
  vi.stubGlobal('fetch', fetchMock);
  return {
    server,
    writes: () => server.calls.filter((call) => call.method !== 'GET'),
    gets: (prefix: string) =>
      server.calls.filter((call) => call.method === 'GET' && call.url.startsWith(prefix)),
  };
}

function LocationProbe() {
  const location = useLocation();
  return <p data-testid="location">{`${location.pathname}${location.search}`}</p>;
}

function renderAt(path: string) {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="products" element={<ProductListPage />} />
          <Route path="products/new" element={<ProductFormPage />} />
          <Route path="products/:id" element={<ProductFormPage />} />
        </Routes>
        <LocationProbe />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function type(label: string, value: string) {
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
}

function click(name: string) {
  fireEvent.click(screen.getByRole('button', { name }));
}

const FIGURES = category('figures', { name: 'Фигури' });
const DRAGONS = category('dragons', { name: 'Дракони', parentId: 'figures' });
const HIDDEN = category('hidden', { name: 'Скрити', isActive: false });
const CATEGORY_LIST = [FIGURES, DRAGONS, HIDDEN];

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('Product list', () => {
  it('shows every status in words, the lowest price of the active variants and the stock', async () => {
    fakeApi(
      [
        product('alpha', {
          name: 'Алфа',
          status: 'Published',
          variants: [
            variant('a1', { price: 2500, stockQuantity: 3 }),
            variant('a2', { price: 1250, stockQuantity: 4 }),
            variant('a3', { price: 100, stockQuantity: 50, isActive: false }),
          ],
        }),
        product('beta', { name: 'Бета', status: 'Draft' }),
        product('gamma', { name: 'Гама', status: 'Archived', fulfilmentType: 'MadeToOrder' }),
      ],
      CATEGORY_LIST,
    );
    renderAt('/products');

    const table = await screen.findByRole('table', { name: 'Списък с продукти' });
    const alpha = within(table).getByRole('row', { name: /Алфа/ });
    expect(alpha).toHaveTextContent('Публикуван');
    expect(alpha).toHaveTextContent('Фигури');
    // 12.50 EUR: the lowest price of the active variants, as the shop currency shows it.
    expect(alpha).toHaveTextContent(/12,50/);
    expect(alpha).toHaveTextContent('7');
    expect(within(table).getByRole('row', { name: /Бета/ })).toHaveTextContent('Чернова');
    expect(within(table).getByRole('row', { name: /Бета/ })).toHaveTextContent(
      'няма активен вариант',
    );
    expect(within(table).getByRole('row', { name: /Гама/ })).toHaveTextContent('Архивиран');
    expect(within(table).getByRole('row', { name: /Гама/ })).toHaveTextContent('По поръчка');
  });

  it('gives each edit link a name that says which product it is for', async () => {
    fakeApi([product('alpha', { name: 'Алфа' })], CATEGORY_LIST);
    renderAt('/products');

    expect(await screen.findByRole('link', { name: 'Редакция на „Алфа“' })).toHaveAttribute(
      'href',
      '/products/alpha',
    );
  });

  it('asks for the status chosen, and keeps it in the address', async () => {
    const api = fakeApi(
      [
        product('alpha', { name: 'Алфа', status: 'Published' }),
        product('beta', { name: 'Бета', status: 'Draft' }),
      ],
      CATEGORY_LIST,
    );
    renderAt('/products');
    await screen.findByRole('table');

    fireEvent.change(screen.getByLabelText('Състояние'), { target: { value: 'Draft' } });

    await waitFor(() => {
      expect(screen.queryByText('Алфа')).not.toBeInTheDocument();
    });
    expect(screen.getByText('Бета')).toBeInTheDocument();
    expect(api.gets(PRODUCTS).at(-1)?.url).toContain('status=Draft');
    expect(screen.getByTestId('location')).toHaveTextContent('/products?status=Draft');
  });

  it('filters by category and by a part of the name, and can clear the filters', async () => {
    const api = fakeApi(
      [
        product('alpha', { name: 'Дракон', categoryId: 'figures' }),
        product('beta', { name: 'Дракончета', categoryId: 'dragons' }),
        product('gamma', { name: 'Робот', categoryId: 'figures' }),
      ],
      CATEGORY_LIST,
    );
    renderAt('/products');
    await screen.findByRole('table');

    fireEvent.change(screen.getByLabelText('Категория'), { target: { value: 'figures' } });
    await waitFor(() => {
      expect(screen.queryByText('Дракончета')).not.toBeInTheDocument();
    });
    type('Търсене по име', 'дракон');
    click('Търси');

    await waitFor(() => {
      expect(screen.queryByText('Робот')).not.toBeInTheDocument();
    });
    expect(screen.getByText('Дракон')).toBeInTheDocument();
    const last = api.gets(PRODUCTS).at(-1)?.url ?? '';
    expect(last).toContain('categoryId=figures');
    expect(last).toContain(`search=${encodeURIComponent('дракон')}`);

    click('Изчисти филтрите');

    expect(await screen.findByText('Робот')).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent(/^\/products$/);
    expect(screen.getByLabelText('Търсене по име')).toHaveValue('');
  });

  it('says so when the filters match nothing, and when there are no products at all', async () => {
    fakeApi([product('alpha', { name: 'Алфа' })], CATEGORY_LIST);
    renderAt('/products?search=zzz');

    expect(await screen.findByText('Няма продукти по тези филтри.')).toBeInTheDocument();
  });

  it('says so when there are no products yet', async () => {
    fakeApi([], CATEGORY_LIST);
    renderAt('/products');

    expect(await screen.findByText(/Още няма продукти/)).toBeInTheDocument();
  });

  it('shows 20 products a page and goes to the next page', async () => {
    const api = fakeApi(
      Array.from({ length: 45 }, (_, index) =>
        product(`p${String(index).padStart(2, '0')}`, {
          name: `Продукт ${String(index).padStart(2, '0')}`,
        }),
      ),
      CATEGORY_LIST,
    );
    renderAt('/products');

    expect(await screen.findByText('Страница 1 от 3')).toBeInTheDocument();
    expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(21);
    expect(screen.getByRole('button', { name: 'Предишна страница' })).toBeDisabled();

    click('Следваща страница');

    expect(await screen.findByText('Страница 2 от 3')).toBeInTheDocument();
    expect(api.gets(PRODUCTS).at(-1)?.url).toContain('page=2');
    expect(screen.getByTestId('location')).toHaveTextContent('page=2');
  });

  it('ignores a page in the address that is not a number', async () => {
    const api = fakeApi([product('alpha', { name: 'Алфа' })], CATEGORY_LIST);
    renderAt('/products?page=abc&status=Deleted');

    expect(await screen.findByText('Алфа')).toBeInTheDocument();
    expect(api.gets(PRODUCTS)[0]?.url).toBe(`${PRODUCTS}?page=1&pageSize=20`);
  });

  it('says so and offers another try when the list cannot be loaded', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(Response.json({ code: 'x' }, { status: 500 }))),
    );
    renderAt('/products');

    expect(await screen.findByRole('alert')).toHaveTextContent('Продуктите не се заредиха');
    expect(screen.getByRole('button', { name: 'Опитай отново' })).toBeInTheDocument();
  });
});

describe('Product form', () => {
  it('shows the problems next to their fields, moves the focus to the first one and sends nothing', async () => {
    const api = fakeApi([], CATEGORY_LIST);
    renderAt('/products/new');
    await screen.findByLabelText('Име');

    click('Запази');

    expect(screen.getByLabelText('Категория')).toHaveFocus();
    expect(screen.getByLabelText('Категория')).toHaveAccessibleDescription('Избери категория.');
    expect(screen.getByLabelText('Име')).toHaveAccessibleDescription('Въведи име.');
    expect(screen.getByLabelText('Адрес (слъг)')).toHaveAccessibleDescription(/Въведи адрес\./);
    expect(screen.getByLabelText('Име')).toBeInvalid();
    expect(api.writes()).toEqual([]);
  });

  it('suggests the slug from the name until the slug is edited', async () => {
    fakeApi([], CATEGORY_LIST);
    renderAt('/products/new');
    await screen.findByLabelText('Име');

    type('Име', 'Син дракон');
    expect(screen.getByLabelText('Адрес (слъг)')).toHaveValue('sin-drakon');

    type('Адрес (слъг)', 'moy-drakon');
    type('Име', 'Червен дракон');
    expect(screen.getByLabelText('Адрес (слъг)')).toHaveValue('moy-drakon');
  });

  it('needs a lead time of at least one day for a made-to-order product', async () => {
    fakeApi([], CATEGORY_LIST);
    renderAt('/products/new');
    await screen.findByLabelText('Име');
    type('Категория', 'figures');
    type('Име', 'Дракон');

    fireEvent.change(screen.getByLabelText('Вид наличност'), { target: { value: 'MadeToOrder' } });
    type('Срок за изработка (дни)', '0');
    click('Запази');

    expect(screen.getByLabelText('Срок за изработка (дни)')).toHaveFocus();
    expect(screen.getByLabelText('Срок за изработка (дни)')).toHaveAccessibleDescription(/поне 1/);
  });

  it('creates the product and opens its page, where the variants are added', async () => {
    const api = fakeApi([], CATEGORY_LIST);
    renderAt('/products/new');
    await screen.findByLabelText('Име');

    type('Категория', 'figures');
    type('Име', 'Дракон');
    type('Кратко описание', 'Кратко');
    click('Запази');

    expect(await screen.findByRole('heading', { name: /Редакция на продукт/ })).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent('/products/new-0');
    expect(api.writes()).toEqual([
      {
        method: 'POST',
        url: PRODUCTS,
        body: {
          categoryId: 'figures',
          name: 'Дракон',
          slug: 'drakon',
          shortDescription: 'Кратко',
          description: '',
          fulfilmentType: 'InStock',
          leadTimeDays: null,
          seoTitle: null,
          seoDescription: null,
        },
      },
    ]);
    expect(screen.getByText(/Още няма варианти/)).toBeInTheDocument();
  });

  it('puts "this slug is taken" next to the slug field', async () => {
    fakeApi([product('drakon', { slug: 'drakon' })], CATEGORY_LIST);
    renderAt('/products/new');
    await screen.findByLabelText('Име');
    type('Категория', 'figures');
    type('Име', 'Дракон');

    click('Запази');

    await waitFor(() => {
      expect(screen.getByLabelText('Адрес (слъг)')).toHaveAccessibleDescription(
        /вече се ползва от друг продукт/,
      );
    });
    expect(screen.getByLabelText('Адрес (слъг)')).toHaveFocus();
    expect(screen.getByLabelText('Адрес (слъг)')).toBeInvalid();
  });

  it('warns that a product in an inactive category is not shown in the shop', async () => {
    fakeApi([], CATEGORY_LIST);
    renderAt('/products/new');
    await screen.findByLabelText('Име');

    type('Категория', 'hidden');

    expect(screen.getByLabelText('Категория')).toHaveAccessibleDescription(
      /няма да се вижда в магазина/,
    );
  });

  it('shows a subcategory with its parent in front, after the parent', async () => {
    fakeApi([], CATEGORY_LIST);
    renderAt('/products/new');
    await screen.findByLabelText('Име');

    const options = within(screen.getByLabelText('Категория'))
      .getAllByRole('option')
      .map((option) => option.textContent);

    expect(options).toEqual(['Избери категория', 'Фигури', 'Фигури / Дракони', 'Скрити']);
  });

  it('saves the changes of a product with the version it was loaded with, and goes back to the list', async () => {
    const api = fakeApi([product('alpha', { name: 'Алфа', version: 7 })], CATEGORY_LIST);
    renderAt('/products/alpha');
    await screen.findByDisplayValue('Алфа');

    type('Име', 'Алфа 2');
    click('Запази');

    await waitFor(() => {
      expect(screen.getByTestId('location')).toHaveTextContent('/products');
    });
    expect(api.writes()[0]).toMatchObject({
      method: 'PUT',
      url: `${PRODUCTS}/alpha`,
      body: { name: 'Алфа 2', slug: 'alpha', version: 7 },
    });
  });

  it('warns that changing the slug of a published product breaks its links', async () => {
    fakeApi([product('alpha', { name: 'Алфа', status: 'Published' })], CATEGORY_LIST);
    renderAt('/products/alpha');
    await screen.findByDisplayValue('Алфа');

    expect(screen.getByLabelText('Адрес (слъг)')).not.toHaveAccessibleDescription(
      /спира да работи/,
    );
    type('Адрес (слъг)', 'alfa');

    expect(screen.getByLabelText('Адрес (слъг)')).toHaveAccessibleDescription(
      /старият адрес спира да работи/,
    );
  });

  it('says so and offers to reload when someone else saved the product first', async () => {
    const api = fakeApi([product('alpha', { name: 'Алфа', version: 7 })], CATEGORY_LIST);
    renderAt('/products/alpha');
    await screen.findByDisplayValue('Алфа');
    const stored = api.server.products.at(0);
    if (stored) {
      stored.name = 'От друг';
      stored.version = 8;
    }

    type('Име', 'Моя промяна');
    click('Запази');

    expect(await screen.findByRole('alert')).toHaveTextContent('Някой друг промени този продукт');
    click('Презареди');

    expect(await screen.findByDisplayValue('От друг')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('says so when the product does not exist', async () => {
    fakeApi([], CATEGORY_LIST);
    renderAt('/products/missing');

    expect(await screen.findByRole('alert')).toHaveTextContent('Този продукт не съществува.');
    expect(screen.getByRole('link', { name: 'Назад към продуктите' })).toBeInTheDocument();
  });

  describe('changing the fulfilment type', () => {
    it('clears the stock when an in-stock product becomes made-to-order, and needs a lead time', async () => {
      const api = fakeApi(
        [product('alpha', { name: 'Алфа', variants: [variant('a1', { stockQuantity: 4 })] })],
        CATEGORY_LIST,
      );
      renderAt('/products/alpha');
      await screen.findByDisplayValue('Алфа');

      fireEvent.change(screen.getByLabelText('Вид наличност'), {
        target: { value: 'MadeToOrder' },
      });
      expect(screen.getByLabelText('Вид наличност')).toHaveAccessibleDescription(
        /наличността на всички варианти се изчиства/,
      );
      type('Срок за изработка (дни)', '7');
      click('Запази');

      await waitFor(() => {
        expect(api.writes()).toHaveLength(1);
      });
      expect(api.writes()[0]?.body).toMatchObject({
        fulfilmentType: 'MadeToOrder',
        leadTimeDays: 7,
      });
      expect(api.server.products[0]?.variants[0]?.stockQuantity).toBeNull();
    });

    it('asks for the stock of every variant when a made-to-order product becomes in-stock', async () => {
      const api = fakeApi(
        [
          product('alpha', {
            name: 'Алфа',
            fulfilmentType: 'MadeToOrder',
            leadTimeDays: 7,
            variants: [
              variant('a1', { name: 'Син', stockQuantity: null }),
              variant('a2', { name: 'Червен', stockQuantity: null, sortOrder: 1 }),
            ],
          }),
        ],
        CATEGORY_LIST,
      );
      renderAt('/products/alpha');
      await screen.findByDisplayValue('Алфа');

      fireEvent.change(screen.getByLabelText('Вид наличност'), { target: { value: 'InStock' } });
      type('Наличност на „Син“', '3');
      click('Запази');

      expect(screen.getByLabelText('Наличност на „Червен“')).toHaveFocus();
      expect(screen.getByLabelText('Наличност на „Червен“')).toHaveAccessibleDescription(
        /цяло число, 0 или повече/,
      );
      expect(api.writes()).toEqual([]);

      type('Наличност на „Червен“', '0');
      click('Запази');

      await waitFor(() => {
        expect(api.writes()).toHaveLength(1);
      });
      expect(api.writes()[0]?.body).toMatchObject({
        fulfilmentType: 'InStock',
        leadTimeDays: null,
        variantStock: { a1: 3, a2: 0 },
      });
    });
  });
});

describe('Product status', () => {
  it('publishes a draft that has an active variant, without asking, with the version', async () => {
    const api = fakeApi(
      [product('alpha', { name: 'Алфа', version: 4, variants: [variant('a1')] })],
      CATEGORY_LIST,
    );
    renderAt('/products/alpha');

    fireEvent.click(await screen.findByRole('button', { name: 'Публикувай' }));

    expect(await screen.findByText(/Сега: Публикуван/)).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(api.writes()).toEqual([
      { method: 'PUT', url: `${PRODUCTS}/alpha/status`, body: { status: 'Published', version: 4 } },
    ]);
  });

  it('says why a product without an active variant cannot be published', async () => {
    fakeApi(
      [product('alpha', { name: 'Алфа', variants: [variant('a1', { isActive: false })] })],
      CATEGORY_LIST,
    );
    renderAt('/products/alpha');

    fireEvent.click(await screen.findByRole('button', { name: 'Публикувай' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('поне един активен вариант');
    expect(screen.getByText(/Сега: Чернова/)).toBeInTheDocument();
  });

  it('asks before archiving a published product, and does nothing when the answer is "Отказ"', async () => {
    const api = fakeApi(
      [product('alpha', { name: 'Алфа', status: 'Published', variants: [variant('a1')] })],
      CATEGORY_LIST,
    );
    renderAt('/products/alpha');

    fireEvent.click(await screen.findByRole('button', { name: 'Архивирай' }));
    const dialog = screen.getByRole('dialog', { name: 'Да архивираме ли „Алфа“?' });
    expect(dialog).toHaveTextContent('Продуктът ще изчезне от магазина');
    expect(dialog).toHaveTextContent('Нищо не се изтрива');
    expect(within(dialog).getByRole('button', { name: 'Отказ' })).toHaveFocus();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Отказ' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(api.writes()).toEqual([]);
  });

  it('archives a published product after the confirmation', async () => {
    const api = fakeApi(
      [
        product('alpha', {
          name: 'Алфа',
          status: 'Published',
          version: 2,
          variants: [variant('a1')],
        }),
      ],
      CATEGORY_LIST,
    );
    renderAt('/products/alpha');

    fireEvent.click(await screen.findByRole('button', { name: 'Архивирай' }));
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Архивирай' }));

    expect(await screen.findByText(/Сега: Архивиран/)).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(api.writes()).toEqual([
      { method: 'PUT', url: `${PRODUCTS}/alpha/status`, body: { status: 'Archived', version: 2 } },
    ]);
  });

  it('asks before returning a published product to draft', async () => {
    fakeApi(
      [product('alpha', { name: 'Алфа', status: 'Published', variants: [variant('a1')] })],
      CATEGORY_LIST,
    );
    renderAt('/products/alpha');

    fireEvent.click(await screen.findByRole('button', { name: 'Върни в чернова' }));

    expect(
      screen.getByRole('dialog', { name: 'Да върнем ли „Алфа“ в чернова?' }),
    ).toBeInTheDocument();
  });

  it('does not ask before archiving a draft, because nothing is hidden', async () => {
    const api = fakeApi(
      [product('alpha', { name: 'Алфа', variants: [variant('a1')] })],
      CATEGORY_LIST,
    );
    renderAt('/products/alpha');

    fireEvent.click(await screen.findByRole('button', { name: 'Архивирай' }));

    expect(await screen.findByText(/Сега: Архивиран/)).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(api.writes()).toHaveLength(1);
  });

  it('says so and offers to reload when someone else changed the product first', async () => {
    const api = fakeApi(
      [product('alpha', { name: 'Алфа', version: 3, variants: [variant('a1')] })],
      CATEGORY_LIST,
    );
    renderAt('/products/alpha');
    await screen.findByRole('button', { name: 'Публикувай' });
    const stored = api.server.products.at(0);
    if (stored) {
      stored.version = 4;
    }

    click('Публикувай');

    expect(await screen.findByRole('alert')).toHaveTextContent('Някой друг промени този продукт');
    expect(screen.getByRole('button', { name: 'Презареди' })).toBeInTheDocument();
  });
});

describe('Variants', () => {
  const WITH_TWO = () =>
    product('alpha', {
      name: 'Алфа',
      status: 'Published',
      variants: [
        variant('a1', { name: 'Син', sku: 'DRG-1', price: 1500, sortOrder: 0 }),
        variant('a2', {
          name: 'Червен',
          sku: 'DRG-2',
          price: 1700,
          sortOrder: 1,
          stockQuantity: 0,
        }),
      ],
    });

  it('lists the variants with price, stock and the inactive ones marked in words', async () => {
    fakeApi(
      [
        product('alpha', {
          name: 'Алфа',
          variants: [
            variant('a1', { name: 'Син', sku: 'DRG-1', price: 1500, compareAtPrice: 1800 }),
            variant('a2', { name: 'Червен', sku: 'DRG-2', isActive: false, sortOrder: 1 }),
          ],
        }),
      ],
      CATEGORY_LIST,
    );
    renderAt('/products/alpha');

    const list = await screen.findByRole('list', { name: 'Варианти на продукта' });
    const [first, second] = within(list).getAllByRole('listitem');
    expect(first).toHaveTextContent('Син');
    expect(first).toHaveTextContent('DRG-1');
    expect(first).toHaveTextContent(/15,00/);
    expect(first).toHaveTextContent(/преди .*18,00/);
    expect(first).toHaveTextContent('наличност: 5');
    expect(second).toHaveTextContent('Неактивен');
  });

  it('gives every button of a variant a name that says which one it is for', async () => {
    fakeApi([WITH_TWO()], CATEGORY_LIST);
    renderAt('/products/alpha');

    expect(
      await screen.findByRole('button', { name: 'Премести „Син“ надолу' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Премести „Червен“ нагоре' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Редакция на „Син“' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Деактивирай „Син“' })).toBeInTheDocument();
    // The first cannot go up and the last cannot go down.
    expect(screen.getByRole('button', { name: 'Премести „Син“ нагоре' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Премести „Червен“ надолу' })).toBeDisabled();
  });

  it('adds a variant: the price is typed in the main unit and sent in the smallest one', async () => {
    const api = fakeApi([product('alpha', { name: 'Алфа' })], CATEGORY_LIST);
    renderAt('/products/alpha');

    fireEvent.click(await screen.findByRole('button', { name: 'Добави вариант' }));
    expect(screen.getByLabelText('Артикулен номер (SKU)')).toHaveFocus();
    type('Артикулен номер (SKU)', 'DRG-1');
    type('Име на варианта', 'Син');
    type('Цена (EUR)', '12,50');
    type('Стара цена (EUR)', '15');
    type('Наличност (брой)', '8');
    type('Атрибути', 'color=син\nsize=M');
    click('Запази варианта');

    expect(await screen.findByRole('list', { name: 'Варианти на продукта' })).toHaveTextContent(
      'Син',
    );
    expect(screen.queryByRole('form', { name: /Нов вариант/ })).not.toBeInTheDocument();
    expect(api.writes()).toEqual([
      {
        method: 'POST',
        url: `${PRODUCTS}/alpha/variants`,
        body: {
          sku: 'DRG-1',
          name: 'Син',
          price: 1250,
          compareAtPrice: 1500,
          stockQuantity: 8,
          weightGrams: 0,
          attributes: { color: 'син', size: 'M' },
          isActive: true,
          sortOrder: 0,
        },
      },
    ]);
    // The focus does not get lost when the form closes.
    expect(screen.getByRole('heading', { name: 'Варианти' })).toHaveFocus();
  });

  it('puts a new variant after the last one', async () => {
    const api = fakeApi([WITH_TWO()], CATEGORY_LIST);
    renderAt('/products/alpha');

    fireEvent.click(await screen.findByRole('button', { name: 'Добави вариант' }));
    type('Артикулен номер (SKU)', 'DRG-3');
    type('Име на варианта', 'Зелен');
    type('Цена (EUR)', '1');
    type('Наличност (брой)', '1');
    click('Запази варианта');

    await waitFor(() => {
      expect(api.writes()).toHaveLength(1);
    });
    expect(api.writes()[0]?.body).toMatchObject({ sortOrder: 2 });
  });

  it('shows the problems of the variant form next to their fields and sends nothing', async () => {
    const api = fakeApi([product('alpha', { name: 'Алфа' })], CATEGORY_LIST);
    renderAt('/products/alpha');
    fireEvent.click(await screen.findByRole('button', { name: 'Добави вариант' }));

    type('Цена (EUR)', '12.345');
    type('Стара цена (EUR)', 'abc');
    click('Запази варианта');

    expect(screen.getByLabelText('Артикулен номер (SKU)')).toHaveFocus();
    expect(screen.getByLabelText('Артикулен номер (SKU)')).toHaveAccessibleDescription(
      /Въведи артикулен номер/,
    );
    expect(screen.getByLabelText('Цена (EUR)')).toHaveAccessibleDescription(/Въведи цена/);
    expect(screen.getByLabelText('Стара цена (EUR)')).toHaveAccessibleDescription(
      /Въведи стара цена/,
    );
    expect(screen.getByLabelText('Наличност (брой)')).toHaveAccessibleDescription(/цяло число/);
    expect(api.writes()).toEqual([]);
  });

  it('refuses an old price below the price, next to its field', async () => {
    fakeApi([product('alpha', { name: 'Алфа' })], CATEGORY_LIST);
    renderAt('/products/alpha');
    fireEvent.click(await screen.findByRole('button', { name: 'Добави вариант' }));

    type('Артикулен номер (SKU)', 'A');
    type('Име на варианта', 'A');
    type('Цена (EUR)', '10');
    type('Стара цена (EUR)', '9.99');
    type('Наличност (брой)', '1');
    click('Запази варианта');

    expect(screen.getByLabelText('Стара цена (EUR)')).toHaveFocus();
    expect(screen.getByLabelText('Стара цена (EUR)')).toHaveAccessibleDescription(
      /не може да е по-малка от цената/,
    );
  });

  it('puts "this SKU is taken" next to the SKU field', async () => {
    fakeApi(
      [
        product('alpha', { name: 'Алфа' }),
        product('beta', { name: 'Бета', variants: [variant('b1', { sku: 'TAKEN' })] }),
      ],
      CATEGORY_LIST,
    );
    renderAt('/products/alpha');
    fireEvent.click(await screen.findByRole('button', { name: 'Добави вариант' }));

    type('Артикулен номер (SKU)', 'TAKEN');
    type('Име на варианта', 'Син');
    type('Цена (EUR)', '5');
    type('Наличност (брой)', '1');
    click('Запази варианта');

    await waitFor(() => {
      expect(screen.getByLabelText('Артикулен номер (SKU)')).toHaveAccessibleDescription(
        /вече се ползва/,
      );
    });
    expect(screen.getByLabelText('Артикулен номер (SKU)')).toHaveFocus();
  });

  it('has no stock field for a made-to-order product, and sends none', async () => {
    const api = fakeApi(
      [product('alpha', { name: 'Алфа', fulfilmentType: 'MadeToOrder', leadTimeDays: 5 })],
      CATEGORY_LIST,
    );
    renderAt('/products/alpha');

    fireEvent.click(await screen.findByRole('button', { name: 'Добави вариант' }));
    expect(screen.queryByLabelText('Наличност (брой)')).not.toBeInTheDocument();
    type('Артикулен номер (SKU)', 'A');
    type('Име на варианта', 'A');
    type('Цена (EUR)', '5');
    click('Запази варианта');

    await waitFor(() => {
      expect(api.writes()).toHaveLength(1);
    });
    expect(api.writes()[0]?.body).toMatchObject({ stockQuantity: null });
  });

  it('opens a variant for editing with its price in the main unit, and saves the change', async () => {
    const api = fakeApi([WITH_TWO()], CATEGORY_LIST);
    renderAt('/products/alpha');

    fireEvent.click(await screen.findByRole('button', { name: 'Редакция на „Син“' }));
    expect(screen.getByLabelText('Цена (EUR)')).toHaveValue('15.00');
    expect(screen.getByLabelText('Артикулен номер (SKU)')).toHaveValue('DRG-1');
    type('Цена (EUR)', '16,99');
    click('Запази варианта');

    await waitFor(() => {
      expect(api.writes()).toHaveLength(1);
    });
    expect(api.writes()[0]).toMatchObject({
      method: 'PUT',
      url: `${PRODUCTS}/alpha/variants/a1`,
      body: { sku: 'DRG-1', price: 1699, stockQuantity: 5 },
    });
    expect(await screen.findByText(/16,99/)).toBeInTheDocument();
  });

  it('closes the form with "Отказ" and sends nothing', async () => {
    const api = fakeApi([WITH_TWO()], CATEGORY_LIST);
    renderAt('/products/alpha');
    fireEvent.click(await screen.findByRole('button', { name: 'Редакция на „Син“' }));

    click('Отказ');

    expect(screen.queryByLabelText('Артикулен номер (SKU)')).not.toBeInTheDocument();
    expect(api.writes()).toEqual([]);
  });

  it('deactivates and activates a variant', async () => {
    const api = fakeApi([WITH_TWO()], CATEGORY_LIST);
    renderAt('/products/alpha');

    fireEvent.click(await screen.findByRole('button', { name: 'Деактивирай „Червен“' }));

    expect(await screen.findByRole('button', { name: 'Активирай „Червен“' })).toBeInTheDocument();
    expect(screen.getByText('Неактивен')).toBeInTheDocument();
    click('Активирай „Червен“');
    expect(await screen.findByRole('button', { name: 'Деактивирай „Червен“' })).toBeInTheDocument();
    expect(api.writes().map((call) => [call.url, call.body])).toEqual([
      [`${PRODUCTS}/alpha/variants/a2/active`, { isActive: false }],
      [`${PRODUCTS}/alpha/variants/a2/active`, { isActive: true }],
    ]);
  });

  it('says why the last active variant of a published product cannot be deactivated', async () => {
    fakeApi(
      [
        product('alpha', {
          name: 'Алфа',
          status: 'Published',
          variants: [variant('a1', { name: 'Син' })],
        }),
      ],
      CATEGORY_LIST,
    );
    renderAt('/products/alpha');

    fireEvent.click(await screen.findByRole('button', { name: 'Деактивирай „Син“' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'последния активен вариант на публикуван продукт',
    );
    expect(screen.getByRole('button', { name: 'Деактивирай „Син“' })).toBeInTheDocument();
  });

  it('moves a variant with the buttons, and the new order is shown', async () => {
    const api = fakeApi([WITH_TWO()], CATEGORY_LIST);
    renderAt('/products/alpha');

    fireEvent.click(await screen.findByRole('button', { name: 'Премести „Червен“ нагоре' }));

    await waitFor(() => {
      const rows = within(screen.getByRole('list', { name: 'Варианти на продукта' })).getAllByRole(
        'listitem',
      );
      expect(rows[0]).toHaveTextContent('Червен');
    });
    expect(screen.getByRole('button', { name: 'Премести „Червен“ нагоре' })).toBeDisabled();
    expect(api.writes()).toEqual([
      { method: 'POST', url: `${PRODUCTS}/alpha/variants/a2/move`, body: { direction: 'Up' } },
    ]);
  });
});
