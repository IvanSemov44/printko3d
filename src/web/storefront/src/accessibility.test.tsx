import { fireEvent, screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { findAccessibilityViolations } from './testing/axe';
import {
  categoriesFixture,
  findContent,
  imageFixture,
  pageFixture,
  productDetailsFixture,
  productFixture,
  renderApp,
  settingsFixture,
  stubNetwork,
  variantFixture,
} from './testing/renderApp';

interface Stub {
  settings?: () => Response | Promise<Response>;
  products?: (url: URL) => Response;
  product?: (slug: string) => Response;
  config?: Record<string, unknown>;
}

function stub({ settings, products, product, config }: Stub = {}) {
  return stubNetwork({
    settings: settings ?? (() => Response.json(settingsFixture())),
    categories: () => Response.json(categoriesFixture()),
    products:
      products ??
      ((url) =>
        Response.json(
          pageFixture(
            [
              productFixture(),
              productFixture({
                id: '11',
                name: 'Puzzle cube',
                slug: 'puzzle-cube',
                isAvailable: false,
              }),
              productFixture({
                id: '12',
                name: 'Made to order',
                slug: 'made',
                fulfilmentType: 'MadeToOrder',
                leadTimeDays: 7,
                primaryImage: null,
              }),
            ],
            { page: Number(url.searchParams.get('page')), totalPages: 5, totalCount: 60 },
          ),
        )),
    product: product ?? (() => Response.json(productDetailsFixture())),
    config,
  });
}

/**
 * Opens the address, waits for the one heading of the page and for what the page shows (`shows`), then checks the
 * whole document. Without `shows` a page that failed to load would pass: the heading is there in that state too.
 */
async function expectAccessible(path: string, heading: string, shows?: () => Promise<unknown>) {
  renderApp(path);
  await screen.findByRole('heading', { level: 1, name: heading });
  await shows?.();
  expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
  expect(await findAccessibilityViolations()).toEqual([]);
}

const cards = async () => {
  const [productList] = await within(await findContent()).findAllByRole('list');
  if (productList === undefined) throw new Error('The page has no list');
  return within(productList).getAllByRole('listitem');
};

const problem = (status: number) => () => Response.json({ title: 'x', code: 'x' }, { status });

describe('accessibility (axe, WCAG 2.1 A and AA)', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe('home page', () => {
    it('has no violations', async () => {
      stub();
      await expectAccessible('/', 'Направено слой по слой');
    });

    it('has none in a demo store (with the banner)', async () => {
      stub({ settings: () => Response.json(settingsFixture({ isDemo: true })) });
      await expectAccessible('/', 'Направено слой по слой');
    });

    it('has none without contacts in the footer', async () => {
      stub({
        settings: () => Response.json(settingsFixture({ contactEmail: '', contactPhone: '' })),
      });
      await expectAccessible('/', 'Направено слой по слой');
    });
  });

  describe('category page', () => {
    it('has no violations with products, placeholders and paging', async () => {
      stub();
      await expectAccessible('/categories/puzzles?page=3', 'Puzzles', async () => {
        expect(await cards()).toHaveLength(3);
        await screen.findByRole('navigation', { name: 'Страниране' });
      });
    });

    it('has none with images from the image address', async () => {
      stub({
        config: { imageBaseUrl: 'http://img.test' },
        products: () =>
          Response.json(
            pageFixture([
              productFixture({
                primaryImage: { storageKey: 'demo/a.jpg', altText: 'Dragon front' },
              }),
              productFixture({
                id: '13',
                name: 'No description of the picture',
                slug: 'no-alt',
                primaryImage: { storageKey: 'demo/b.jpg', altText: '' },
              }),
            ]),
          ),
      });
      await expectAccessible('/categories/puzzles', 'Puzzles', async () => {
        expect(await cards()).toHaveLength(2);
        expect(screen.getByRole('img', { name: 'Dragon front' })).toBeInTheDocument();
        expect(
          screen.getByRole('img', { name: 'No description of the picture' }),
        ).toBeInTheDocument();
      });
    });

    it('has none for an empty category', async () => {
      stub({ products: () => Response.json(pageFixture([])) });
      await expectAccessible('/categories/puzzles', 'Puzzles', async () => {
        await screen.findByText('В тази категория още няма продукти');
      });
    });

    it('has none when the products cannot be loaded', async () => {
      stub({ products: problem(500) });
      renderApp('/categories/puzzles');
      await screen.findByRole('alert');
      expect(await findAccessibilityViolations()).toEqual([]);
    });
  });

  describe('product page', () => {
    it('has no violations for a product with one variant and no pictures', async () => {
      stub();
      await expectAccessible('/products/dragon', 'Dragon');
    });

    it('has none with several variants, a gallery and a sold out variant', async () => {
      stub({
        config: { imageBaseUrl: 'http://img.test' },
        product: () =>
          Response.json(
            productDetailsFixture({
              variants: [
                variantFixture({ id: 'v1', sku: 'S', name: 'Small' }),
                variantFixture({
                  id: 'v2',
                  sku: 'L',
                  name: 'Large',
                  isAvailable: false,
                  compareAtPrice: 4000,
                }),
              ],
              images: [
                imageFixture({ id: 'a', sortOrder: 1, isPrimary: true }),
                imageFixture({ id: 'b', storageKey: 'demo/two.jpg', altText: '', sortOrder: 2 }),
              ],
            }),
          ),
      });
      await expectAccessible('/products/dragon', 'Dragon');
    });

    it('has none after another variant is chosen and another picture shown', async () => {
      stub({
        config: { imageBaseUrl: 'http://img.test' },
        product: () =>
          Response.json(
            productDetailsFixture({
              variants: [
                variantFixture({ id: 'v1', sku: 'S', name: 'Small' }),
                variantFixture({ id: 'v2', sku: 'L', name: 'Large' }),
              ],
              images: [
                imageFixture({ id: 'a', sortOrder: 1 }),
                imageFixture({
                  id: 'b',
                  storageKey: 'demo/two.jpg',
                  altText: 'Second',
                  sortOrder: 2,
                }),
              ],
            }),
          ),
      });
      renderApp('/products/dragon');
      await screen.findByRole('heading', { level: 1, name: 'Dragon' });

      fireEvent.click(screen.getByRole('radio', { name: 'Large' }));
      fireEvent.click(screen.getByRole('button', { name: 'Second' }));

      expect(await findAccessibilityViolations()).toEqual([]);
    });

    it('has none for a made-to-order product', async () => {
      stub({
        product: () =>
          Response.json(productDetailsFixture({ fulfilmentType: 'MadeToOrder', leadTimeDays: 5 })),
      });
      await expectAccessible('/products/dragon', 'Dragon');
    });

    it('has none when the product cannot be loaded', async () => {
      stub({ product: problem(500) });
      renderApp('/products/dragon');
      await screen.findByRole('alert');
      expect(await findAccessibilityViolations()).toEqual([]);
    });
  });

  describe('missing pages', () => {
    it('has no violations for an unknown address', async () => {
      stub();
      await expectAccessible('/nothing-here', 'Страницата не е намерена');
    });

    it('has none for a missing product', async () => {
      stub({ product: problem(404) });
      await expectAccessible('/products/unknown', 'Страницата не е намерена');
    });

    it('has none for a missing category', async () => {
      stub();
      await expectAccessible('/categories/unknown', 'Страницата не е намерена');
    });
  });

  describe('the screens before the shop is there', () => {
    it('has no violations while the settings load', async () => {
      stub({ settings: () => new Promise<Response>(() => undefined) });
      renderApp('/');
      await screen.findByRole('status');
      expect(await findAccessibilityViolations()).toEqual([]);
    });

    it('has none when the settings cannot be loaded', async () => {
      stub({ settings: problem(500) });
      renderApp('/');
      await screen.findByRole('alert');
      expect(await findAccessibilityViolations()).toEqual([]);
    });
  });

  it('keeps the keyboard order simple: the skip link first, no positive tabindex', async () => {
    stub();
    renderApp('/categories/puzzles');
    const main = await findContent();
    await within(main).findByRole('heading', { level: 1 });

    const links = screen.getAllByRole('link');
    expect(links[0]).toHaveAttribute('href', '#content');
    expect(
      document.querySelectorAll('[tabindex]:not([tabindex="-1"]):not([tabindex="0"])'),
    ).toHaveLength(0);
  });
});
