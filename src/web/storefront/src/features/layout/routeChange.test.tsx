import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  categoriesFixture,
  findContent,
  pageFixture,
  productDetailsFixture,
  productFixture,
  renderApp,
  settingsFixture,
  stubNetwork,
} from '../../testing/renderApp';

function stub() {
  return stubNetwork({
    settings: () => Response.json(settingsFixture()),
    categories: () => Response.json(categoriesFixture()),
    products: () => Response.json(pageFixture([productFixture()])),
    product: () => Response.json(productDetailsFixture()),
  });
}

function announcement(): string {
  return screen.getByTestId('route-announcement').textContent;
}

describe('moving between pages (a single-page app does not reload, so the page has to say so)', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('does not move the focus or announce anything when the first page opens', async () => {
    stub();
    renderApp('/categories/puzzles');
    await screen.findByRole('heading', { level: 1, name: 'Puzzles' });

    expect(document.body).toHaveFocus();
    expect(announcement()).toBe('');
  });

  it('moves the focus to the content and announces the heading of the new page', async () => {
    stub();
    renderApp('/');
    await screen.findByRole('heading', { level: 1, name: 'Направено слой по слой' });

    fireEvent.click(
      within(await screen.findByRole('navigation', { name: 'Категории' })).getByRole('link', {
        name: 'Puzzles',
      }),
    );

    await waitFor(() => {
      expect(announcement()).toBe('Puzzles');
    });
    expect(await findContent()).toHaveFocus();
  });

  it('waits for the content of a page that has to load before it announces', async () => {
    stub();
    renderApp('/categories/puzzles');
    const list = await within(await findContent()).findByRole('list');

    fireEvent.click(within(list).getByRole('link', { name: 'Dragon' }));

    await waitFor(() => {
      expect(announcement()).toBe('Dragon');
    });
    expect(await findContent()).toHaveFocus();
  });

  it('announces the home page when the user goes back to it by the name of the shop', async () => {
    stub();
    renderApp('/products/dragon');
    await screen.findByRole('heading', { level: 1, name: 'Dragon' });

    fireEvent.click(screen.getByRole('link', { name: 'Test Shop' }));
    await waitFor(() => {
      expect(announcement()).toBe('Направено слой по слой');
    });
  });

  it('leaves the focus alone when only the page number changes', async () => {
    stubNetwork({
      settings: () => Response.json(settingsFixture()),
      categories: () => Response.json(categoriesFixture()),
      products: (url) =>
        Response.json(
          pageFixture([productFixture()], {
            page: Number(url.searchParams.get('page')),
            totalPages: 3,
            totalCount: 30,
          }),
        ),
    });
    renderApp('/categories/puzzles');
    const nav = await screen.findByRole('navigation', { name: 'Страниране' });

    fireEvent.click(within(nav).getByRole('link', { name: 'Следваща' }));

    await waitFor(() => {
      expect(screen.getByRole('heading', { level: 1, name: 'Puzzles' })).toHaveFocus();
    });
    expect(announcement()).toBe('');
  });
});
