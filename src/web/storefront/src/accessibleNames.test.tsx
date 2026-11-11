import { screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
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
} from './testing/renderApp';

const IMAGE_BASE = 'http://img.test';

function stub(options: { products?: unknown[]; product?: Record<string, unknown> }) {
  return stubNetwork({
    settings: () => Response.json(settingsFixture()),
    categories: () => Response.json(categoriesFixture()),
    products: () => Response.json(pageFixture(options.products ?? [])),
    product: () => Response.json(productDetailsFixture(options.product)),
    config: { imageBaseUrl: IMAGE_BASE },
  });
}

describe('names of links and pictures', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('gives a product without a picture one link, not a second one with nothing to read', async () => {
    stub({ products: [productFixture()] });
    renderApp('/categories/puzzles');
    const list = await within(await findContent()).findByRole('list');

    const links = within(list).getAllByRole('link');

    expect(links.map((link) => link.textContent)).toEqual(['Dragon']);
  });

  it('names the picture of a card after the product when the picture has no text of its own', async () => {
    stub({
      products: [productFixture({ primaryImage: { storageKey: 'demo/a.jpg', altText: '' } })],
    });
    renderApp('/categories/puzzles');
    const list = await within(await findContent()).findByRole('list');

    expect(within(list).getByRole('img', { name: 'Dragon' })).toBeInTheDocument();
    // The picture is a link too, and the picture has the name of the product.
    expect(within(list).getAllByRole('link', { name: 'Dragon' })).toHaveLength(2);
  });

  it('keeps the text of the picture when it has one', async () => {
    stub({
      products: [
        productFixture({ primaryImage: { storageKey: 'demo/a.jpg', altText: 'Dragon front' } }),
      ],
    });
    renderApp('/categories/puzzles');
    const list = await within(await findContent()).findByRole('list');

    expect(within(list).getByRole('img', { name: 'Dragon front' })).toBeInTheDocument();
  });

  it('names the large picture of a product after the product when the picture has no text of its own', async () => {
    stub({
      product: { images: [imageFixture({ altText: '', isPrimary: true })] },
    });
    renderApp('/products/dragon');
    await screen.findByRole('heading', { level: 1, name: 'Dragon' });

    const gallery = screen.getByRole('region', { name: 'Снимки на продукта' });
    expect(within(gallery).getByRole('img', { name: 'Dragon' })).toBeInTheDocument();
  });
});
