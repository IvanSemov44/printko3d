import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../queryClient';
import { AdminLayout } from './AdminLayout';

function renderLayout(path = '/products') {
  vi.stubGlobal(
    'fetch',
    vi.fn(() =>
      Promise.resolve(
        Response.json({ storeName: 'Test shop', defaultCurrency: 'EUR', defaultLocale: 'bg' }),
      ),
    ),
  );
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter initialEntries={[path]}>
        <AdminLayout footer={<p>footer text</p>}>
          <h2>Page title</h2>
        </AdminLayout>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('AdminLayout', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('lists the screens that exist, marks the open one and shows the page and the footer', () => {
    renderLayout('/products');

    const menu = screen.getByRole('navigation', { name: 'Основно меню' });
    const names = Array.from(menu.querySelectorAll('a')).map((link) => link.textContent);
    expect(names).toEqual(['Поръчки', 'Продукти', 'Категории', 'Доставка', 'Настройки']);
    expect(screen.getByRole('link', { name: 'Продукти' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', { name: 'Поръчки' })).not.toHaveAttribute('aria-current');
    expect(screen.getByRole('heading', { name: 'Page title' })).toBeInTheDocument();
    expect(screen.getByText('footer text')).toBeInTheDocument();
  });

  it('shows the name of the shop from the settings, not a name from the code', async () => {
    renderLayout();

    await waitFor(() => {
      expect(screen.getAllByText('Test shop').length).toBeGreaterThan(0);
    });
  });

  it('opens and closes the menu with the button, and closes it with Escape', async () => {
    const user = userEvent.setup();
    renderLayout();
    const button = screen.getByRole('button', { name: 'Меню' });
    expect(button).toHaveAttribute('aria-expanded', 'false');

    await user.click(button);
    expect(button).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('complementary')).toHaveAttribute('data-open', 'true');

    await user.keyboard('{Escape}');
    expect(button).toHaveAttribute('aria-expanded', 'false');

    await user.click(button);
    await user.click(screen.getByRole('link', { name: 'Категории' }));
    expect(button).toHaveAttribute('aria-expanded', 'false');
  });
});
