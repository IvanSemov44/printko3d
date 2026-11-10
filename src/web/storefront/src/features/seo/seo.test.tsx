import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  categoriesFixture,
  imageFixture,
  pageFixture,
  productDetailsFixture,
  productFixture,
  renderApp,
  settingsFixture,
  stubNetwork,
  variantFixture,
} from '../../testing/renderApp';

const ORIGIN = window.location.origin;

function stub(
  options: {
    settings?: Record<string, unknown>;
    product?: Record<string, unknown>;
    productStatus?: number;
    config?: Record<string, unknown>;
  } = {},
) {
  return stubNetwork({
    settings: () => Response.json(settingsFixture(options.settings)),
    categories: () => Response.json(categoriesFixture()),
    products: (url) => {
      const page = Number(url.searchParams.get('page'));
      return Response.json(
        pageFixture([productFixture()], { page, totalPages: 3, totalCount: 30 }),
      );
    },
    product: () =>
      options.productStatus === undefined
        ? Response.json(productDetailsFixture(options.product))
        : Response.json({ title: 'Not found' }, { status: options.productStatus }),
    config: options.config,
  });
}

function meta(name: string): string | null {
  return document.head.querySelector(`meta[name="${name}"]`)?.getAttribute('content') ?? null;
}

function property(name: string): string | null {
  return document.head.querySelector(`meta[property="${name}"]`)?.getAttribute('content') ?? null;
}

function canonical(): string | null {
  return document.head.querySelector('link[rel="canonical"]')?.getAttribute('href') ?? null;
}

function jsonLd(): Record<string, unknown> {
  const script = document.querySelector('script[type="application/ld+json"]');
  if (script === null) {
    throw new Error('No JSON-LD on the page');
  }
  return JSON.parse(script.textContent) as Record<string, unknown>;
}

function offer(): Record<string, unknown> {
  return jsonLd().offers as Record<string, unknown>;
}

async function openProduct(
  options: Parameters<typeof stub>[0] = {},
  path = '/products/dragon',
): Promise<void> {
  stub(options);
  renderApp(path);
  await screen.findByRole('heading', { level: 1, name: 'Dragon' });
}

describe('page meta', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe('home page', () => {
    it('has the store name as title, a description and a canonical address', async () => {
      stub({ settings: { storeName: 'Another Shop' } });
      renderApp('/');
      await screen.findByRole('heading', { level: 1 });

      expect(document.title).toBe('Another Shop');
      expect(meta('description')).toBe('Разгледайте продуктите на Another Shop.');
      expect(canonical()).toBe(`${ORIGIN}/`);
      expect(meta('robots')).toBeNull();
    });
  });

  describe('category page', () => {
    it('uses the category name and description', async () => {
      stub();
      renderApp('/categories/puzzles');
      await screen.findByRole('heading', { level: 1, name: 'Puzzles' });

      expect(document.title).toBe('Puzzles – Test Shop');
      expect(meta('description')).toBe('Wooden and printed puzzles');
      expect(canonical()).toBe(`${ORIGIN}/categories/puzzles`);
    });

    it('uses a text from the translations when the category has no description', async () => {
      stub();
      renderApp('/categories/figurines');
      await screen.findByRole('heading', { level: 1, name: 'Figurines' });

      expect(meta('description')).toBe('Продукти от категория Figurines в Test Shop.');
    });

    it('cuts a long description to 160 characters', async () => {
      stubNetwork({
        settings: () => Response.json(settingsFixture()),
        categories: () =>
          Response.json([
            { ...categoriesFixture()[0], description: 'long description '.repeat(30) },
          ]),
        products: () => Response.json(pageFixture([productFixture()])),
      });
      renderApp('/categories/puzzles');
      await screen.findByRole('heading', { level: 1, name: 'Puzzles' });

      const description = meta('description') ?? '';
      expect(description.length).toBeLessThanOrEqual(160);
      expect(description.endsWith('…')).toBe(true);
    });

    it('does not repeat the title of the first page on the next pages', async () => {
      stub();
      renderApp('/categories/puzzles?page=2');
      await screen.findByRole('heading', { level: 1, name: 'Puzzles' });

      expect(document.title).toBe('Puzzles – Страница 2 – Test Shop');
      expect(canonical()).toBe(`${ORIGIN}/categories/puzzles?page=2`);
    });

    it('drops parameters that do not change the page from the canonical address', async () => {
      stub();
      renderApp('/categories/puzzles?utm_source=mail&page=1');
      await screen.findByRole('heading', { level: 1, name: 'Puzzles' });

      expect(canonical()).toBe(`${ORIGIN}/categories/puzzles`);
    });

    it('changes the title when the user moves to the next page', async () => {
      stub();
      renderApp('/categories/puzzles');
      await screen.findByRole('heading', { level: 1, name: 'Puzzles' });
      expect(document.title).toBe('Puzzles – Test Shop');

      const nav = await screen.findByRole('navigation', { name: 'Страниране' });
      fireEvent.click(within(nav).getByRole('link', { name: 'Следваща' }));

      await waitFor(() => {
        expect(document.title).toBe('Puzzles – Страница 2 – Test Shop');
      });
    });
  });

  describe('product page', () => {
    it('uses seoTitle and seoDescription from the API', async () => {
      await openProduct({ product: { seoTitle: 'Buy the dragon', seoDescription: 'Best dragon' } });

      expect(document.title).toBe('Buy the dragon – Test Shop');
      expect(meta('description')).toBe('Best dragon');
      expect(canonical()).toBe(`${ORIGIN}/products/dragon`);
    });

    it('falls back to the name and the short description', async () => {
      await openProduct();

      expect(document.title).toBe('Dragon – Test Shop');
      expect(meta('description')).toBe('A printed dragon');
    });

    it('shares the title, description, address and main image', async () => {
      await openProduct({
        product: {
          images: [
            imageFixture({ id: 'a', storageKey: 'demo/second.jpg', sortOrder: 2 }),
            imageFixture({ id: 'b', storageKey: 'demo/main.jpg', sortOrder: 1, isPrimary: true }),
          ],
        },
        config: { imageBaseUrl: 'http://img.test' },
      });

      expect(property('og:type')).toBe('product');
      expect(property('og:title')).toBe('Dragon – Test Shop');
      expect(property('og:description')).toBe('A printed dragon');
      expect(property('og:url')).toBe(`${ORIGIN}/products/dragon`);
      expect(property('og:image')).toBe('http://img.test/demo/main.jpg');
      expect(meta('twitter:card')).toBe('summary_large_image');
      expect(meta('twitter:image')).toBe('http://img.test/demo/main.jpg');
    });

    it('has no image tags for a product without an image', async () => {
      await openProduct();

      expect(property('og:image')).toBeNull();
      expect(meta('twitter:card')).toBe('summary');
    });
  });

  describe('structured data', () => {
    it('describes the cheapest variant with the currency from the settings', async () => {
      await openProduct({
        settings: { defaultCurrency: 'BGN' },
        product: {
          variants: [
            variantFixture({ id: 'v1', sku: 'DRG-L', price: 3000, sortOrder: 1 }),
            variantFixture({ id: 'v2', sku: 'DRG-S', price: 1250, sortOrder: 2 }),
          ],
        },
      });

      const data = jsonLd();
      expect(data['@type']).toBe('Product');
      expect(data.name).toBe('Dragon');
      expect(data.sku).toBe('DRG-S');
      expect(offer().price).toBe('12.50');
      expect(offer().priceCurrency).toBe('BGN');
      expect(offer().availability).toBe('https://schema.org/InStock');
    });

    it('does not say "in stock" for a sold out product', async () => {
      await openProduct({ product: { variants: [variantFixture({ isAvailable: false })] } });

      expect(offer().availability).toBe('https://schema.org/OutOfStock');
    });

    it('says "pre-order" for a made-to-order product', async () => {
      await openProduct({
        product: {
          fulfilmentType: 'MadeToOrder',
          variants: [variantFixture({ isAvailable: false })],
        },
      });

      expect(offer().availability).toBe('https://schema.org/PreOrder');
    });

    it('cannot be closed early by text in the description', async () => {
      await openProduct({ product: { description: '</script><b>x</b>' } });

      expect(document.querySelectorAll('script[type="application/ld+json"]')).toHaveLength(1);
      expect(jsonLd().description).toBe('</script><b>x</b>');
    });
  });

  describe('pages kept out of search results', () => {
    it('marks an unknown address as not found and without a canonical address', async () => {
      stub();
      renderApp('/nothing-here');
      await screen.findByRole('heading', { level: 1, name: 'Страницата не е намерена' });

      expect(document.title).toBe('Страницата не е намерена – Test Shop');
      expect(meta('robots')).toBe('noindex');
      expect(canonical()).toBeNull();
    });

    it('marks a missing product the same way', async () => {
      stub({ productStatus: 404 });
      renderApp('/products/unknown');
      await screen.findByRole('heading', { level: 1, name: 'Страницата не е намерена' });

      expect(meta('robots')).toBe('noindex');
      expect(canonical()).toBeNull();
    });

    it('marks a missing category the same way', async () => {
      stub();
      renderApp('/categories/unknown');
      await screen.findByRole('heading', { level: 1, name: 'Страницата не е намерена' });

      expect(meta('robots')).toBe('noindex');
    });

    it('keeps every page of a demo store out of the index', async () => {
      await openProduct({ settings: { isDemo: true } });

      expect(meta('robots')).toBe('noindex');
    });

    it('does not add noindex to the pages of a real store', async () => {
      await openProduct();

      expect(meta('robots')).toBeNull();
    });
  });
});
