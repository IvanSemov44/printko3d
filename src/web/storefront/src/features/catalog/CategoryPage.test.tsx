import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  categoriesFixture,
  pageFixture,
  productFixture,
  renderApp,
  settingsFixture,
  stubNetwork,
} from '../../testing/renderApp';
import { plain } from '../../testing/text';

const ok = (body: unknown) => () => Response.json(body);

function stub(
  products: (url: URL) => Response | Promise<Response>,
  settings: Record<string, unknown> = {},
) {
  return stubNetwork({
    settings: ok(settingsFixture(settings)),
    categories: ok(categoriesFixture()),
    products,
  });
}

/** Query strings of all product requests made so far. */
function productRequests(fetchMock: ReturnType<typeof stub>): URLSearchParams[] {
  return fetchMock.mock.calls
    .filter(([url]) => url.startsWith('http://api.test/api/v1/products?'))
    .map(([url]) => new URL(url))
    .map((url) => url.searchParams);
}

function lastPageRequested(fetchMock: ReturnType<typeof stub>): string | null {
  return productRequests(fetchMock).at(-1)?.get('page') ?? null;
}

/** The product list is the only list inside the main content. */
async function findProductList(): Promise<HTMLElement> {
  return within(await screen.findByRole('main')).findByRole('list');
}

const threePages = (url: URL) => {
  const page = Number(url.searchParams.get('page'));
  return Response.json(
    pageFixture([productFixture({ name: `Item on page ${String(page)}` })], {
      page,
      totalPages: 3,
      totalCount: 30,
    }),
  );
};

describe('category page', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the category name and its products as links to the product pages', async () => {
    const fetchMock = stub(() =>
      Response.json(
        pageFixture([
          productFixture(),
          productFixture({ id: '11', name: 'Puzzle cube', slug: 'puzzle-cube' }),
        ]),
      ),
    );
    renderApp('/categories/puzzles');

    expect(await screen.findByRole('heading', { level: 1, name: 'Puzzles' })).toBeInTheDocument();
    const items = within(await findProductList()).getAllByRole('listitem');
    expect(items).toHaveLength(2);
    expect(within(items[0]).getByRole('link', { name: 'Dragon' })).toHaveAttribute(
      'href',
      '/products/dragon',
    );
    expect(within(items[0]).getByText('A printed dragon')).toBeInTheDocument();
    expect(productRequests(fetchMock)[0]?.get('category')).toBe('puzzles');
    expect(productRequests(fetchMock)[0]?.get('pageSize')).toBe('12');
  });

  it('formats the price with the currency and locale from the settings', async () => {
    stub(() => Response.json(pageFixture([productFixture({ minPrice: 123456 })])), {
      defaultCurrency: 'USD',
      defaultLocale: 'en-US',
    });
    renderApp('/categories/puzzles');

    const price = await screen.findByText(/1,234\.56/);
    expect(plain(price.textContent)).toBe('от $1,234.56');
  });

  it('shows euro prices for the default settings', async () => {
    stub(() => Response.json(pageFixture([productFixture({ minPrice: 1250 })])));
    renderApp('/categories/puzzles');

    const price = await screen.findByText(/12,50/);
    expect(plain(price.textContent)).toBe('от 12,50 €');
  });

  it('shows the three kinds of availability', async () => {
    stub(() =>
      Response.json(
        pageFixture([
          productFixture({ id: '1', name: 'A', slug: 'a' }),
          productFixture({ id: '2', name: 'B', slug: 'b', isAvailable: false }),
          productFixture({
            id: '3',
            name: 'C',
            slug: 'c',
            fulfilmentType: 'MadeToOrder',
            leadTimeDays: 5,
          }),
          productFixture({
            id: '4',
            name: 'D',
            slug: 'd',
            fulfilmentType: 'MadeToOrder',
            leadTimeDays: 1,
          }),
          productFixture({
            id: '5',
            name: 'E',
            slug: 'e',
            fulfilmentType: 'MadeToOrder',
            isAvailable: false,
          }),
        ]),
      ),
    );
    renderApp('/categories/puzzles');

    const items = within(await findProductList()).getAllByRole('listitem');
    expect(items[0]).toHaveTextContent('В наличност');
    expect(items[1]).toHaveTextContent('Изчерпан');
    expect(items[2]).toHaveTextContent('По поръчка · 5 дни');
    expect(items[3]).toHaveTextContent('По поръчка · 1 ден');
    expect(items[4]).toHaveTextContent('По поръчка');
    expect(items[4]).not.toHaveTextContent('дни');
    expect(items[4]).not.toHaveTextContent('Изчерпан');
  });

  describe('page number', () => {
    it('asks for the page from the address and marks it as current', async () => {
      const fetchMock = stub(threePages);
      renderApp('/categories/puzzles?page=2');

      expect(await screen.findByText('Item on page 2')).toBeInTheDocument();
      expect(lastPageRequested(fetchMock)).toBe('2');
      const nav = screen.getByRole('navigation', { name: 'Страниране' });
      expect(within(nav).getByRole('link', { name: 'Страница 2' })).toHaveAttribute(
        'aria-current',
        'page',
      );
      expect(within(nav).getByRole('link', { name: 'Страница 1' })).not.toHaveAttribute(
        'aria-current',
      );
    });

    it.each(['', '?page=abc', '?page=0', '?page=-4', '?page=1.5'])(
      'shows page 1 for %j',
      async (search) => {
        const fetchMock = stub(threePages);
        renderApp(`/categories/puzzles${search}`);

        expect(await screen.findByText('Item on page 1')).toBeInTheDocument();
        expect(productRequests(fetchMock).map((query) => query.get('page'))).toEqual(['1']);
      },
    );

    it('moves from a page after the last one to the last page', async () => {
      const fetchMock = stub((url) =>
        Number(url.searchParams.get('page')) > 3
          ? Response.json(pageFixture([], { page: 99, totalPages: 3, totalCount: 30 }))
          : threePages(url),
      );
      renderApp('/categories/puzzles?page=99');

      expect(await screen.findByText('Item on page 3')).toBeInTheDocument();
      expect(lastPageRequested(fetchMock)).toBe('3');
      const nav = screen.getByRole('navigation', { name: 'Страниране' });
      expect(within(nav).getByRole('link', { name: 'Страница 3' })).toHaveAttribute(
        'aria-current',
        'page',
      );
    });

    it('moves to page 1 when a category has only one page', async () => {
      const fetchMock = stub((url) => {
        const page = Number(url.searchParams.get('page'));
        return Response.json(
          pageFixture(page === 1 ? [productFixture()] : [], { page, totalPages: 1, totalCount: 1 }),
        );
      });
      renderApp('/categories/puzzles?page=5');

      expect(await screen.findByRole('link', { name: 'Dragon' })).toBeInTheDocument();
      expect(lastPageRequested(fetchMock)).toBe('1');
    });

    it('does not read the page size from the address', async () => {
      const fetchMock = stub(threePages);
      renderApp('/categories/puzzles?pageSize=48');

      await screen.findByText('Item on page 1');
      expect(productRequests(fetchMock)[0]?.get('pageSize')).toBe('12');
    });

    it('goes to the next page with the right request, then moves focus to the heading', async () => {
      const scrollSpy = vi.spyOn(Element.prototype, 'scrollIntoView');
      const fetchMock = stub(threePages);
      renderApp('/categories/puzzles');

      await screen.findByText('Item on page 1');
      const nav = screen.getByRole('navigation', { name: 'Страниране' });
      const next = within(nav).getByRole('link', { name: 'Следваща' });
      expect(next).toHaveAttribute('href', '/categories/puzzles?page=2');
      fireEvent.click(next);

      expect(await screen.findByText('Item on page 2')).toBeInTheDocument();
      const query = productRequests(fetchMock).at(-1);
      expect(query?.get('category')).toBe('puzzles');
      expect(query?.get('page')).toBe('2');
      await waitFor(() => {
        expect(screen.getByRole('heading', { level: 1 })).toHaveFocus();
      });
      expect(scrollSpy).toHaveBeenCalled();
    });

    it('keeps the old list visible while the next page loads', async () => {
      let release: () => void = () => undefined;
      const gate = new Promise<void>((resolve) => {
        release = resolve;
      });
      stub(async (url) => {
        if (url.searchParams.get('page') === '2') {
          await gate;
        }
        return threePages(url);
      });
      renderApp('/categories/puzzles');

      await screen.findByText('Item on page 1');
      fireEvent.click(screen.getByRole('link', { name: 'Следваща' }));

      expect(screen.getByText('Item on page 1')).toBeInTheDocument();
      await waitFor(() => {
        expect(
          within(screen.getByRole('main')).getByRole('list', { busy: true }),
        ).toBeInTheDocument();
      });

      release();
      expect(await screen.findByText('Item on page 2')).toBeInTheDocument();
    });

    it('has no link for Previous on the first page and none for Next on the last', async () => {
      stub(threePages);
      renderApp('/categories/puzzles');

      await screen.findByText('Item on page 1');
      expect(screen.queryByRole('link', { name: 'Предишна' })).not.toBeInTheDocument();
      expect(screen.getByText('Предишна')).toHaveAttribute('aria-disabled', 'true');
      fireEvent.click(screen.getByRole('link', { name: 'Страница 3' }));

      await screen.findByText('Item on page 3');
      expect(screen.queryByRole('link', { name: 'Следваща' })).not.toBeInTheDocument();
      expect(screen.getByText('Следваща')).toHaveAttribute('aria-disabled', 'true');
      expect(screen.getByRole('link', { name: 'Предишна' })).toBeInTheDocument();
    });

    it('hides the paging when there is only one page', async () => {
      stub(() => Response.json(pageFixture([productFixture()])));
      renderApp('/categories/puzzles');

      await screen.findByRole('link', { name: 'Dragon' });
      expect(screen.queryByRole('navigation', { name: 'Страниране' })).not.toBeInTheDocument();
    });
  });

  describe('states', () => {
    it('says so when the category has no products', async () => {
      stub(() => Response.json(pageFixture([])));
      renderApp('/categories/puzzles');

      expect(await screen.findByText('В тази категория още няма продукти')).toBeInTheDocument();
      expect(screen.getByRole('heading', { level: 1, name: 'Puzzles' })).toBeInTheDocument();
      expect(within(screen.getByRole('main')).queryByRole('list')).not.toBeInTheDocument();
    });

    it('shows the not-found page for an unknown slug, without asking for products', async () => {
      const fetchMock = stub(() => Response.json(pageFixture([productFixture()])));
      renderApp('/categories/nothing');

      expect(
        await screen.findByRole('heading', { name: 'Страницата не е намерена' }),
      ).toBeInTheDocument();
      expect(productRequests(fetchMock)).toHaveLength(0);
    });

    it('finds a subcategory by its slug too', async () => {
      const fetchMock = stub(() => Response.json(pageFixture([productFixture()])));
      renderApp('/categories/small-puzzles');

      expect(
        await screen.findByRole('heading', { level: 1, name: 'Small puzzles' }),
      ).toBeInTheDocument();
      expect(productRequests(fetchMock)[0]?.get('category')).toBe('small-puzzles');
    });

    it('shows a loading state while the products load', async () => {
      let release: () => void = () => undefined;
      const gate = new Promise<void>((resolve) => {
        release = resolve;
      });
      stub(() => gate.then(() => Response.json(pageFixture([productFixture()]))));
      renderApp('/categories/puzzles');

      expect(await screen.findByRole('heading', { level: 1, name: 'Puzzles' })).toBeInTheDocument();
      expect(screen.getByRole('status')).toHaveTextContent('Зареждане…');

      release();
      expect(await screen.findByRole('link', { name: 'Dragon' })).toBeInTheDocument();
      expect(screen.queryByRole('status')).not.toBeInTheDocument();
    });

    it('shows a general message and retries, keeping the rest of the page', async () => {
      let failing = true;
      stub(() =>
        failing
          ? Response.json({ code: 'validation.failed', detail: 'secret' }, { status: 400 })
          : Response.json(pageFixture([productFixture()])),
      );
      renderApp('/categories/puzzles');

      const alert = await screen.findByRole('alert');
      expect(alert).toHaveTextContent('Продуктите не могат да се заредят');
      expect(alert).not.toHaveTextContent('validation.failed');
      expect(alert).not.toHaveTextContent('secret');
      expect(screen.getByRole('banner')).toBeInTheDocument();
      expect(screen.getByRole('navigation', { name: 'Категории' })).toBeInTheDocument();
      expect(screen.getByRole('contentinfo')).toBeInTheDocument();

      failing = false;
      fireEvent.click(screen.getByRole('button', { name: 'Опитай пак' }));

      expect(await screen.findByRole('link', { name: 'Dragon' })).toBeInTheDocument();
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });

    it('shows the same message when the categories cannot be loaded', async () => {
      stubNetwork({
        settings: ok(settingsFixture()),
        categories: () => new Response(null, { status: 500 }),
        products: () => Response.json(pageFixture([])),
      });
      renderApp('/categories/puzzles');

      expect(await screen.findByRole('alert')).toHaveTextContent(
        'Продуктите не могат да се заредят',
      );
      expect(screen.getByRole('button', { name: 'Опитай пак' })).toBeInTheDocument();
    });
  });
});
