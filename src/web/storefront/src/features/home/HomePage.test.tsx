import { screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  categoriesFixture,
  findContent,
  pageFixture,
  productFixture,
  renderApp,
  settingsFixture,
  stubNetwork,
} from '../../testing/renderApp';

afterEach(() => {
  vi.unstubAllGlobals();
});

function stubHome(products: (category: string) => unknown[]) {
  return stubNetwork({
    settings: () => Response.json(settingsFixture()),
    categories: () => Response.json(categoriesFixture()),
    products: (url) => Response.json(pageFixture(products(url.searchParams.get('category') ?? ''))),
  });
}

describe('home page', () => {
  it('shows the store name, a heading, and a link to the first category', async () => {
    stubHome(() => []);
    renderApp();

    const content = await findContent();
    expect(within(content).getByText('Test Shop')).toBeInTheDocument();
    expect(
      within(content).getByRole('heading', { level: 1, name: 'Направено слой по слой' }),
    ).toBeInTheDocument();
    expect(
      await within(content).findByRole('link', { name: 'Разгледай каталога' }),
    ).toHaveAttribute('href', '/categories/puzzles');
  });

  it('shows a few products from each top-level category, with a link to all of them', async () => {
    const fetchMock = stubHome((category) => [
      productFixture({ id: category, name: `Product of ${category}`, slug: category }),
    ]);
    renderApp();

    const content = await findContent();
    expect(
      await within(content).findByRole('heading', { level: 2, name: 'Puzzles' }),
    ).toBeVisible();
    expect(
      await within(content).findByRole('heading', { level: 2, name: 'Figurines' }),
    ).toBeVisible();
    expect(
      await within(content).findByRole('link', { name: 'Product of puzzles' }),
    ).toHaveAttribute('href', '/products/puzzles');
    expect(within(content).getByRole('link', { name: 'Виж всички в Figurines' })).toHaveAttribute(
      'href',
      '/categories/figurines',
    );
    // Sub-categories are not sections of their own.
    expect(within(content).queryByRole('heading', { name: 'Small puzzles' })).toBeNull();
    // Only a preview is asked for, not a whole page.
    const sizes = fetchMock.mock.calls
      .map(([url]) => url)
      .filter((url) => url.includes('/products?'))
      .map((url) => new URL(url).searchParams.get('pageSize'));
    expect(sizes).toEqual(['4', '4']);
  });

  it('leaves out a category without products and keeps the page working when products fail', async () => {
    stubNetwork({
      settings: () => Response.json(settingsFixture()),
      categories: () => Response.json(categoriesFixture()),
      products: (url) =>
        url.searchParams.get('category') === 'puzzles'
          ? Response.json(pageFixture([]))
          : new Response(null, { status: 500 }),
    });
    renderApp();

    const content = await findContent();
    expect(await within(content).findByRole('link', { name: 'Разгледай каталога' })).toBeVisible();
    await vi.waitFor(() => {
      expect(screen.queryByRole('heading', { level: 2 })).toBeNull();
    });
    expect(
      within(content).getByRole('heading', { level: 1, name: 'Направено слой по слой' }),
    ).toBeInTheDocument();
  });
});
