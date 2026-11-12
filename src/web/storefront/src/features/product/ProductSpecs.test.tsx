import { screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  categoriesFixture,
  findContent,
  pageFixture,
  productDetailsFixture,
  renderApp,
  settingsFixture,
  stubNetwork,
} from '../../testing/renderApp';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('product page data', () => {
  it('shows the code and the weight of the variant, and the availability as a text badge', async () => {
    stubNetwork({
      settings: () => Response.json(settingsFixture()),
      categories: () => Response.json(categoriesFixture()),
      product: () => Response.json(productDetailsFixture()),
    });
    renderApp('/products/dragon');

    const content = await findContent();
    expect(await within(content).findByText('DRG-S')).toBeInTheDocument();
    expect(within(content).getByText('40 г')).toBeInTheDocument();
    expect(within(content).getByText('В наличност')).toHaveClass('badge');
  });
});

describe('empty category', () => {
  it('says so in one sentence and offers one way on', async () => {
    stubNetwork({
      settings: () => Response.json(settingsFixture()),
      categories: () => Response.json(categoriesFixture()),
      products: () => Response.json(pageFixture([])),
    });
    renderApp('/categories/puzzles');

    expect(await screen.findByText('В тази категория още няма продукти')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Разгледай другите категории' })).toHaveAttribute(
      'href',
      '/',
    );
  });
});
