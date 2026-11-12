import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, waitFor } from '@testing-library/react';
import { MemoryRouter, useLocation, useNavigationType } from 'react-router';
import { vi } from 'vitest';
import { App } from '../App';

export function settingsFixture(overrides: Record<string, unknown> = {}) {
  return {
    storeName: 'Test Shop',
    defaultCurrency: 'EUR',
    defaultLocale: 'bg',
    contactEmail: 'shop@example.test',
    contactPhone: '+359 111 222 333',
    logoKey: null,
    primaryColor: '#2563EB',
    isDemo: false,
    ...overrides,
  };
}

export function categoriesFixture() {
  return [
    {
      id: '1',
      parentId: null,
      name: 'Puzzles',
      slug: 'puzzles',
      description: 'Wooden and printed puzzles',
      imageKey: null,
      sortOrder: 1,
    },
    {
      id: '2',
      parentId: null,
      name: 'Figurines',
      slug: 'figurines',
      description: null,
      imageKey: null,
      sortOrder: 2,
    },
    {
      id: '3',
      parentId: '1',
      name: 'Small puzzles',
      slug: 'small-puzzles',
      description: null,
      imageKey: null,
      sortOrder: 1,
    },
  ];
}

export function productFixture(overrides: Record<string, unknown> = {}) {
  return {
    id: '10',
    name: 'Dragon',
    slug: 'dragon',
    shortDescription: 'A printed dragon',
    fulfilmentType: 'InStock',
    leadTimeDays: null,
    minPrice: 1250,
    primaryImage: null,
    isAvailable: true,
    ...overrides,
  };
}

export function variantFixture(overrides: Record<string, unknown> = {}) {
  return {
    id: 'v1',
    sku: 'DRG-S',
    name: 'Small',
    price: 1250,
    compareAtPrice: null,
    isAvailable: true,
    weightGrams: 40,
    attributes: {},
    sortOrder: 1,
    ...overrides,
  };
}

export function imageFixture(overrides: Record<string, unknown> = {}) {
  return {
    id: 'img1',
    variantId: null,
    storageKey: 'demo/dragon-1.jpg',
    altText: 'Dragon front',
    sortOrder: 1,
    isPrimary: false,
    ...overrides,
  };
}

export function productDetailsFixture(overrides: Record<string, unknown> = {}) {
  return {
    id: '10',
    name: 'Dragon',
    slug: 'dragon',
    shortDescription: 'A printed dragon',
    description: 'Printed in PLA. Painted by hand.',
    fulfilmentType: 'InStock',
    leadTimeDays: null,
    seoTitle: '',
    seoDescription: '',
    isAvailable: true,
    variants: [variantFixture()],
    images: [],
    ...overrides,
  };
}

export function pageFixture(
  items: unknown[],
  overrides: { page?: number; totalPages?: number; totalCount?: number } = {},
) {
  return {
    items,
    page: 1,
    pageSize: 12,
    totalCount: items.length,
    totalPages: items.length === 0 ? 0 : 1,
    ...overrides,
  };
}

export function cartLineFixture(overrides: Record<string, unknown> = {}) {
  return {
    variantId: 'v1',
    productName: 'Dragon',
    productSlug: 'dragon',
    variantName: 'Small',
    sku: 'DRG-S',
    image: null,
    unitPrice: 1250,
    quantity: 1,
    linePrice: 1250,
    state: 'Available',
    ...overrides,
  };
}

/** The cart as the server counts it: the sums come from the lines given. */
export function cartFixture(lines: Record<string, unknown>[] = []) {
  return {
    lines,
    itemCount: lines.reduce((sum, line) => sum + Number(line['quantity']), 0),
    total: lines.reduce((sum, line) => sum + Number(line['linePrice']), 0),
  };
}

export type Responder = () => Response | Promise<Response>;

/** Fakes the network at the HTTP boundary: /config.json plus the API paths. Products get the parsed request address. */
export function stubNetwork(routes: {
  settings: Responder;
  categories: Responder;
  products?: (url: URL) => Response | Promise<Response>;
  /** Gets the slug from /api/v1/products/{slug}. */
  product?: (slug: string) => Response | Promise<Response>;
  /** Gets every request to /api/v1/cart. Without it the cart is empty, so a page that only shows the header works. */
  cart?: (url: string, init?: RequestInit) => Response | Promise<Response>;
  /** Extra fields for /config.json, for example imageBaseUrl. */
  config?: Record<string, unknown>;
}) {
  const fetchMock = vi.fn((url: string, init?: RequestInit): Promise<Response> => {
    if (url === '/config.json') {
      return Promise.resolve(Response.json({ apiUrl: 'http://api.test', ...routes.config }));
    }
    if (url.startsWith('/api/v1/cart')) {
      return Promise.resolve(routes.cart ? routes.cart(url, init) : Response.json(cartFixture()));
    }
    if (url === 'http://api.test/api/v1/store-settings') {
      return Promise.resolve(routes.settings());
    }
    if (url === 'http://api.test/api/v1/categories') {
      return Promise.resolve(routes.categories());
    }
    if (routes.products && url.startsWith('http://api.test/api/v1/products?')) {
      return Promise.resolve(routes.products(new URL(url)));
    }
    if (routes.product && url.startsWith('http://api.test/api/v1/products/')) {
      return Promise.resolve(
        routes.product(decodeURIComponent(url.slice('http://api.test/api/v1/products/'.length))),
      );
    }
    return Promise.reject(new TypeError(`Unexpected request: ${url}`));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

/** Hidden element with the current address and how it was reached ("PUSH" or "REPLACE"), for tests. */
function LocationProbe() {
  const location = useLocation();
  const navigationType = useNavigationType();
  return (
    <div hidden data-testid="location">
      {`${location.pathname}${location.search} ${navigationType}`}
    </div>
  );
}

export function renderApp(initialPath = '/') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialPath]}>
        <App />
        <LocationProbe />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

/**
 * The content area of the layout. The screen before the shop is there (settings loading) is a `main` too, so a test that
 * wants the page waits for this one, which has the id that the skip link points to.
 */
export async function findContent(): Promise<HTMLElement> {
  return waitFor(() => {
    const content = document.getElementById('content');
    if (content === null) {
      throw new Error('The layout is not shown yet');
    }
    return content;
  });
}
