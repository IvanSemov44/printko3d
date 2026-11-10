import { fireEvent, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { categoriesFixture, renderApp, settingsFixture, stubNetwork } from './testing/renderApp';

const ok = (body: unknown) => () => Response.json(body);

function stubHappyPath(settings: Record<string, unknown> = {}) {
  return stubNetwork({
    settings: ok(settingsFixture(settings)),
    categories: ok(categoriesFixture()),
  });
}

describe('App', () => {
  beforeEach(() => {
    document.documentElement.removeAttribute('style');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe('layout', () => {
    it('shows the store name as a link home, the top-level categories, main content and the footer', async () => {
      stubHappyPath();
      renderApp();

      const header = await screen.findByRole('banner');
      expect(within(header).getByRole('link', { name: 'Test Shop' })).toHaveAttribute('href', '/');

      const menu = await screen.findByRole('navigation', { name: 'Категории' });
      const links = within(menu).getAllByRole('link');
      expect(links.map((link) => link.textContent)).toEqual(['Puzzles', 'Figurines']);
      expect(links[0]).toHaveAttribute('href', '/categories/puzzles');

      const main = screen.getByRole('main');
      expect(within(main).getByRole('heading', { level: 1, name: 'Начало' })).toBeInTheDocument();

      const footer = screen.getByRole('contentinfo');
      expect(within(footer).getByText('Test Shop')).toBeInTheDocument();
      expect(within(footer).getByRole('link', { name: 'shop@example.test' })).toHaveAttribute(
        'href',
        'mailto:shop@example.test',
      );
      expect(within(footer).getByRole('link', { name: '+359 111 222 333' })).toHaveAttribute(
        'href',
        'tel:+359111222333',
      );
    });

    it('omits contacts that are not set', async () => {
      stubHappyPath({ contactEmail: '', contactPhone: '' });
      renderApp();

      const footer = await screen.findByRole('contentinfo');
      expect(within(footer).queryByRole('link')).not.toBeInTheDocument();
    });

    it('starts with a skip link that targets the main content', async () => {
      stubHappyPath();
      renderApp();

      await screen.findByRole('banner');
      const skipLink = screen.getByRole('link', { name: 'Към съдържанието' });
      const allLinks = screen.getAllByRole('link');

      expect(allLinks[0]).toBe(skipLink);
      expect(skipLink).toHaveAttribute('href', '#content');
      expect(screen.getByRole('main')).toHaveAttribute('id', 'content');
    });

    it('still shows the page when the categories cannot be loaded', async () => {
      stubNetwork({
        settings: ok(settingsFixture()),
        categories: () => new Response(null, { status: 500 }),
      });
      renderApp();

      expect(await screen.findByRole('heading', { name: 'Начало' })).toBeInTheDocument();
      expect(screen.queryByRole('navigation', { name: 'Категории' })).not.toBeInTheDocument();
    });

    it('shows a not-found page with a way home for unknown addresses', async () => {
      stubHappyPath();
      renderApp('/nothing-here');

      expect(
        await screen.findByRole('heading', { name: 'Страницата не е намерена' }),
      ).toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'Към началната страница' })).toHaveAttribute(
        'href',
        '/',
      );
    });
  });

  describe('loading and errors', () => {
    it('shows a loading state while the settings load', async () => {
      let release: () => void = () => undefined;
      const gate = new Promise<void>((resolve) => {
        release = resolve;
      });
      stubNetwork({
        settings: () => gate.then(() => Response.json(settingsFixture())),
        categories: ok(categoriesFixture()),
      });

      renderApp();
      expect(screen.getByRole('status')).toHaveTextContent('Зареждане…');
      expect(screen.queryByRole('banner')).not.toBeInTheDocument();

      release();
      expect(await screen.findByRole('banner')).toBeInTheDocument();
      expect(screen.queryByRole('status')).not.toBeInTheDocument();
    });

    it('shows a general message and retries when the settings return 404', async () => {
      let failing = true;
      stubNetwork({
        settings: () =>
          failing
            ? Response.json({ code: 'store_settings.not_found', detail: 'secret' }, { status: 404 })
            : Response.json(settingsFixture()),
        categories: ok(categoriesFixture()),
      });
      renderApp();

      const alert = await screen.findByRole('alert');
      expect(alert).toHaveTextContent('Магазинът не може да се зареди в момента.');
      expect(alert).not.toHaveTextContent('store_settings.not_found');
      expect(alert).not.toHaveTextContent('secret');

      failing = false;
      fireEvent.click(screen.getByRole('button', { name: 'Опитай пак' }));

      expect(await screen.findByRole('banner')).toBeInTheDocument();
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });

    it('shows the same message when the API is unreachable', async () => {
      stubHappyPath();
      vi.mocked(fetch).mockImplementation((input) =>
        input === '/config.json'
          ? Promise.resolve(Response.json({ apiUrl: 'http://api.test' }))
          : Promise.reject(new TypeError('Failed to fetch')),
      );
      renderApp();

      expect(await screen.findByRole('alert')).toHaveTextContent('Магазинът не може да се зареди');
      expect(screen.getByRole('button', { name: 'Опитай пак' })).toBeInTheDocument();
    });
  });

  describe('demo banner', () => {
    it('is shown without a close button when the store is a demo', async () => {
      stubHappyPath({ isDemo: true });
      renderApp();

      const banner = await screen.findByRole('complementary', { name: 'Демо версия' });
      expect(banner).toHaveTextContent('Това е демо версия');
      expect(within(banner).queryByRole('button')).not.toBeInTheDocument();
    });

    it('is not shown when the store is not a demo', async () => {
      stubHappyPath({ isDemo: false });
      renderApp();

      await screen.findByRole('banner');
      expect(screen.queryByRole('complementary')).not.toBeInTheDocument();
    });
  });

  describe('theme and title', () => {
    it('sets the primary colour from the settings with readable text on it', async () => {
      stubHappyPath({ primaryColor: '#0A1F44' });
      renderApp();

      await screen.findByRole('banner');
      const style = document.documentElement.style;
      expect(style.getPropertyValue('--color-primary')).toBe('#0a1f44');
      expect(style.getPropertyValue('--color-on-primary')).toBe('#ffffff');
    });

    it('picks dark text on a light primary colour', async () => {
      stubHappyPath({ primaryColor: '#FFE066' });
      renderApp();

      await screen.findByRole('banner');
      expect(document.documentElement.style.getPropertyValue('--color-on-primary')).toBe('#000000');
    });

    it('uses the default colour and keeps working when the colour is invalid', async () => {
      stubHappyPath({ primaryColor: 'not-a-colour' });
      renderApp();

      expect(await screen.findByRole('banner')).toBeInTheDocument();
      expect(document.documentElement.style.getPropertyValue('--color-primary')).toBe('#2f5d8a');
    });

    it('puts the store name in the document title', async () => {
      stubHappyPath({ storeName: 'Another Shop' });
      renderApp();

      await screen.findByRole('banner');
      expect(document.title).toBe('Another Shop');
    });
  });

  describe('after the settings are edited in the admin panel', () => {
    it('follows the new name, colour, demo bar and contacts when the page is opened again', async () => {
      stubHappyPath({
        storeName: 'Old Shop',
        primaryColor: '#0A1F44',
        isDemo: true,
        contactEmail: 'old@example.test',
        contactPhone: '+359 111 222 333',
      });
      const first = renderApp();
      await screen.findByRole('complementary', { name: 'Демо версия' });
      expect(document.title).toBe('Old Shop');
      expect(document.documentElement.style.getPropertyValue('--color-on-primary')).toBe('#ffffff');
      first.unmount();

      // The administrator saves new settings; the shop shows them after the page is loaded again.
      stubHappyPath({
        storeName: 'New Shop',
        primaryColor: '#FFE066',
        isDemo: false,
        contactEmail: 'new@example.test',
        contactPhone: '',
      });
      renderApp();

      const footer = await screen.findByRole('contentinfo');
      expect(within(footer).getByText('New Shop')).toBeInTheDocument();
      expect(within(footer).getByRole('link', { name: 'new@example.test' })).toBeInTheDocument();
      expect(within(footer).queryByText('+359 111 222 333')).not.toBeInTheDocument();
      expect(document.title).toBe('New Shop');
      const style = document.documentElement.style;
      expect(style.getPropertyValue('--color-primary')).toBe('#ffe066');
      expect(style.getPropertyValue('--color-on-primary')).toBe('#000000');
      expect(screen.queryByRole('complementary', { name: 'Демо версия' })).not.toBeInTheDocument();
    });
  });

  describe('locale', () => {
    it('falls back to Bulgarian texts when the default locale has no translation', async () => {
      stubHappyPath({ defaultLocale: 'de-DE' });
      renderApp();

      expect(await screen.findByRole('heading', { name: 'Начало' })).toBeInTheDocument();
    });
  });
});
