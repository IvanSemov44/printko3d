import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
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
    { id: '1', parentId: null, name: 'Puzzles', slug: 'puzzles', sortOrder: 1 },
    { id: '2', parentId: null, name: 'Figurines', slug: 'figurines', sortOrder: 2 },
    { id: '3', parentId: '1', name: 'Small puzzles', slug: 'small-puzzles', sortOrder: 1 },
  ];
}

export type Responder = () => Response | Promise<Response>;

/** Fakes the network at the HTTP boundary: /config.json plus the two API paths used by the layout. */
export function stubNetwork(routes: { settings: Responder; categories: Responder }) {
  const fetchMock = vi.fn((url: string): Promise<Response> => {
    if (url === '/config.json') {
      return Promise.resolve(Response.json({ apiUrl: 'http://api.test' }));
    }
    if (url === 'http://api.test/api/v1/store-settings') {
      return Promise.resolve(routes.settings());
    }
    if (url === 'http://api.test/api/v1/categories') {
      return Promise.resolve(routes.categories());
    }
    return Promise.reject(new TypeError(`Unexpected request: ${url}`));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

export function renderApp(initialPath = '/') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialPath]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}
