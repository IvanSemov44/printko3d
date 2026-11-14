import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, waitFor } from '@testing-library/react';
import { MemoryRouter, useLocation, useNavigationType } from 'react-router';
import { vi } from 'vitest';
import { App } from '../App';
import { cartFixture } from './fixtures';

// The fixtures live in their own file; they are exported from here too, so a test has one place to import from.
export {
  cartFixture,
  cartLineFixture,
  categoriesFixture,
  imageFixture,
  pageFixture,
  productDetailsFixture,
  productFixture,
  settingsFixture,
  shippingFixture,
  variantFixture,
} from './fixtures';

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
  /** Gets every request to /api/v1/orders. Without it a test that never places an order fails loudly if one is sent. */
  orders?: (url: string, init?: RequestInit) => Response | Promise<Response>;
  /** Gets every request to /api/v1/shipping. Without it a test that never prices a delivery fails loudly if one is sent. */
  shipping?: (url: string, init?: RequestInit) => Response | Promise<Response>;
  /** Gets every request to /api/v1/payments except the mode. Without it a test that never pays fails loudly if one is sent. */
  payments?: (url: string, init?: RequestInit) => Response | Promise<Response>;
  /** Whether the notice of the test payment is shown. It is not unless a test says so. */
  isTestPayment?: boolean;
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
    if (url === '/api/v1/payments/mode') {
      return Promise.resolve(Response.json({ isTest: routes.isTestPayment ?? false }));
    }
    if (routes.payments && url.startsWith('/api/v1/payments')) {
      return Promise.resolve(routes.payments(url, init));
    }
    if (routes.orders && url.startsWith('/api/v1/orders')) {
      return Promise.resolve(routes.orders(url, init));
    }
    if (routes.shipping && url.startsWith('/api/v1/shipping')) {
      return Promise.resolve(routes.shipping(url, init));
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

/** The first address of the test; an entry with a state is how a page that was reached by navigating (with data) is opened. */
export function renderApp(initialPath: string | { pathname: string; state: unknown } = '/') {
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
