import type { AdminCategory, AdminProduct } from '@storefront/shared';
import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import { createQueryClient } from './queryClient';
import { findAccessibilityViolations } from './testing/axe';

const CATEGORIES: AdminCategory[] = [
  {
    id: 'figures',
    parentId: null,
    name: 'Figures',
    slug: 'figures',
    description: 'Printed figures',
    sortOrder: 0,
    isActive: true,
    productCount: 1,
    version: 1,
  },
  {
    id: 'small',
    parentId: 'figures',
    name: 'Small figures',
    slug: 'small',
    description: null,
    sortOrder: 0,
    isActive: false,
    productCount: 0,
    version: 1,
  },
];

const PRODUCT: AdminProduct = {
  id: 'p1',
  categoryId: 'figures',
  name: 'Dragon',
  slug: 'dragon',
  shortDescription: 'A printed dragon',
  description: 'A long description',
  fulfilmentType: 'InStock',
  leadTimeDays: null,
  status: 'Published',
  seoTitle: null,
  seoDescription: null,
  version: 1,
  variants: [
    {
      id: 'v1',
      sku: 'DRAGON-RED',
      name: 'Red',
      price: 2490,
      compareAtPrice: 2990,
      stockQuantity: 5,
      weightGrams: 120,
      attributes: {},
      isActive: true,
      sortOrder: 0,
    },
    {
      id: 'v2',
      sku: 'DRAGON-BLUE',
      name: 'Blue',
      price: 2490,
      compareAtPrice: null,
      stockQuantity: 0,
      weightGrams: 80,
      attributes: {},
      isActive: false,
      sortOrder: 1,
    },
  ],
  images: [
    {
      id: 'i1',
      variantId: null,
      storageKey: 'products/p1/i1.jpg',
      altText: 'A red dragon',
      sortOrder: 0,
      isPrimary: true,
    },
    {
      id: 'i2',
      variantId: 'v1',
      storageKey: 'products/p1/i2.jpg',
      altText: 'A red dragon from the side',
      sortOrder: 1,
      isPrimary: false,
    },
  ],
};

const PRODUCT_LIST_ITEM = {
  id: PRODUCT.id,
  name: PRODUCT.name,
  slug: PRODUCT.slug,
  categoryId: PRODUCT.categoryId,
  categoryName: 'Figures',
  status: PRODUCT.status,
  fulfilmentType: PRODUCT.fulfilmentType,
  variantCount: 2,
  minPrice: 2490,
  totalStock: 5,
  version: PRODUCT.version,
};

const SETTINGS = {
  storeName: 'Test shop',
  defaultCurrency: 'EUR',
  defaultLocale: 'bg',
  contactEmail: 'shop@example.test',
  contactPhone: '+359 2 000 0000',
  logoKey: null,
  primaryColor: '#2f5d8a',
  isDemo: true,
  version: 1,
};

/** Replaces fetch with the answers of a small API, for a signed-in administrator or for a visitor. */
function stubApi({ signedIn }: { signedIn: boolean }) {
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET';
      const [path = ''] = url.split('?');
      const json = (value: unknown) => Promise.resolve(Response.json(value));

      if (path === '/config.json') {
        return json({ apiUrl: 'http://api.test', imageBaseUrl: 'http://images.test' });
      }
      if (path === 'http://api.test/health') {
        return Promise.resolve(new Response(null, { status: 200 }));
      }
      if (path === '/api/v1/auth/me') {
        return signedIn
          ? json({ email: 'admin@example.test' })
          : Promise.resolve(new Response(null, { status: 401 }));
      }
      if (method !== 'GET') {
        return Promise.reject(new Error(`Unexpected request: ${method} ${url}`));
      }
      if (path === '/api/v1/admin/categories') {
        return json(CATEGORIES);
      }
      if (path === '/api/v1/admin/products') {
        return json({
          items: [PRODUCT_LIST_ITEM],
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        });
      }
      if (path === `/api/v1/admin/products/${PRODUCT.id}`) {
        return json(PRODUCT);
      }
      if (path === '/api/v1/admin/store-settings') {
        return json(SETTINGS);
      }
      if (path === '/api/v1/store-settings') {
        return json({ defaultCurrency: 'EUR', defaultLocale: 'bg' });
      }
      return Promise.reject(new Error(`Unexpected request: ${method} ${url}`));
    }),
  );
}

function renderAt(address: string) {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter initialEntries={[address]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

/**
 * Opens the address, waits for what the page shows (`shows`), then checks the whole document. Without waiting for
 * `shows` a page that is still loading, or that failed to load, would pass: the heading is there in those states too.
 */
async function violationsOf(address: string, shows: () => Promise<unknown>, signedIn = true) {
  stubApi({ signedIn });
  renderAt(address);
  await shows();
  return findAccessibilityViolations();
}

describe('accessibility of the admin panel (axe, WCAG 2.1 A and AA)', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('has none on the sign-in form', async () => {
    const violations = await violationsOf('/', () => screen.findByLabelText('Парола'), false);

    expect(violations).toEqual([]);
  });

  it('has none on the list of categories', async () => {
    const violations = await violationsOf('/categories', () => screen.findByText('Small figures'));

    expect(violations).toEqual([]);
  });

  it('has none on the form of a new category', async () => {
    const violations = await violationsOf('/categories/new', () =>
      screen.findByRole('heading', { name: 'Нова категория' }),
    );

    expect(violations).toEqual([]);
  });

  it('has none on the form of an existing category', async () => {
    const violations = await violationsOf('/categories/figures', () =>
      screen.findByDisplayValue('Figures'),
    );

    expect(violations).toEqual([]);
  });

  it('has none on the list of products', async () => {
    const violations = await violationsOf('/products', () => screen.findByText('Dragon'));

    expect(violations).toEqual([]);
  });

  it('has none on the form of a new product', async () => {
    const violations = await violationsOf('/products/new', () =>
      screen.findByRole('heading', { name: 'Нов продукт' }),
    );

    expect(violations).toEqual([]);
  });

  it('has none on the form of an existing product, with its variants and images', async () => {
    const violations = await violationsOf('/products/p1', async () => {
      await screen.findByText(/DRAGON-RED/);
      await screen.findByAltText('A red dragon');
    });

    expect(violations).toEqual([]);
  });

  it('has none on the settings of the store', async () => {
    const violations = await violationsOf('/settings', () =>
      screen.findByDisplayValue('Test shop'),
    );

    expect(violations).toEqual([]);
  });
});
