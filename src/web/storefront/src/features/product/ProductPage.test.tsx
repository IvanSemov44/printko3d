import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  categoriesFixture,
  imageFixture,
  productDetailsFixture,
  renderApp,
  settingsFixture,
  stubNetwork,
  variantFixture,
} from '../../testing/renderApp';
import { plain } from '../../testing/text';

const IMAGE_BASE = 'http://img.test';

function stub(
  product: (slug: string) => Response | Promise<Response>,
  options: { settings?: Record<string, unknown>; config?: Record<string, unknown> } = {},
) {
  return stubNetwork({
    settings: () => Response.json(settingsFixture(options.settings)),
    categories: () => Response.json(categoriesFixture()),
    product,
    config: options.config,
  });
}

function stubProduct(
  overrides: Record<string, unknown> = {},
  options: Parameters<typeof stub>[1] = {},
) {
  return stub(() => Response.json(productDetailsFixture(overrides)), options);
}

function location(): string {
  return screen.getByTestId('location').textContent;
}

async function findMain(): Promise<HTMLElement> {
  return screen.findByRole('main');
}

async function findOffer(): Promise<HTMLElement> {
  await screen.findByRole('heading', { level: 1 });
  const offer = document.querySelector('[aria-live]');
  if (!(offer instanceof HTMLElement)) {
    throw new Error('No live region with the price');
  }
  return offer;
}

const twoVariants = [
  variantFixture({ id: 'v1', sku: 'DRG-S', name: 'Small', price: 1250, sortOrder: 1 }),
  variantFixture({ id: 'v2', sku: 'DRG-L', name: 'Large', price: 2400, sortOrder: 2 }),
];

describe('product page', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe('content', () => {
    it('shows the name, the short description and the full description', async () => {
      const fetchMock = stubProduct();
      renderApp('/products/dragon');

      expect(await screen.findByRole('heading', { level: 1, name: 'Dragon' })).toBeInTheDocument();
      expect(screen.getByText('A printed dragon')).toBeInTheDocument();
      expect(screen.getByText('Printed in PLA. Painted by hand.')).toBeInTheDocument();
      expect(fetchMock).toHaveBeenCalledWith(
        'http://api.test/api/v1/products/dragon',
        expect.anything(),
      );
    });

    it('shows descriptions as plain text, not as HTML', async () => {
      stubProduct({ description: '<b>bold</b> <img src=x onerror=alert(1)>' });
      renderApp('/products/dragon');

      const description = await screen.findByText(/bold/);
      expect(description.textContent).toBe('<b>bold</b> <img src=x onerror=alert(1)>');
      expect(description.querySelector('b')).toBeNull();
      expect(description.querySelector('img')).toBeNull();
    });

    it('shows a loading state while the product loads', async () => {
      stub(() => new Promise<Response>(() => undefined));
      renderApp('/products/dragon');

      expect(await within(await findMain()).findByRole('status')).toHaveTextContent('Зареждане');
      expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
    });
  });

  describe('errors', () => {
    it('shows the not-found page when the API answers 404', async () => {
      stub(() => Response.json({ code: 'product.not_found' }, { status: 404 }));
      renderApp('/products/nothing');

      expect(
        await screen.findByRole('heading', { name: 'Страницата не е намерена' }),
      ).toBeInTheDocument();
    });

    it('shows a general message and keeps the layout on another error, then retries', async () => {
      let calls = 0;
      stub(() => {
        calls += 1;
        return calls === 1
          ? Response.json({ title: 'Boom', detail: 'secret detail' }, { status: 500 })
          : Response.json(productDetailsFixture());
      });
      renderApp('/products/dragon');

      const alert = await screen.findByRole('alert');
      expect(alert).toHaveTextContent('Продуктът не може да се зареди');
      expect(alert).not.toHaveTextContent('secret detail');
      expect(alert).not.toHaveTextContent('500');
      expect(screen.getByRole('banner')).toBeInTheDocument();
      expect(screen.getByRole('contentinfo')).toBeInTheDocument();

      fireEvent.click(within(alert).getByRole('button', { name: 'Опитай пак' }));

      expect(await screen.findByRole('heading', { level: 1, name: 'Dragon' })).toBeInTheDocument();
    });
  });

  describe('price and availability', () => {
    it('formats the price with the currency and locale from the settings', async () => {
      stubProduct(
        { variants: [variantFixture({ price: 123456 })] },
        { settings: { defaultCurrency: 'USD', defaultLocale: 'en-US' } },
      );
      renderApp('/products/dragon');

      expect(plain((await findOffer()).textContent)).toContain('$1,234.56');
    });

    it('shows euro prices for the default settings', async () => {
      stubProduct();
      renderApp('/products/dragon');

      expect(plain((await findOffer()).textContent)).toContain('12,50 €');
    });

    it('shows a struck-through old price with a text for screen readers', async () => {
      stubProduct({ variants: [variantFixture({ price: 1250, compareAtPrice: 1800 })] });
      renderApp('/products/dragon');

      const offer = await findOffer();
      const oldPrice = offer.querySelector('s');
      expect(plain(oldPrice?.textContent ?? null)).toBe('Стара цена: 18,00 €');
      expect(plain(offer.textContent)).toContain('12,50 €');
    });

    it.each([[null], [1250], [900]])('shows no old price for compareAtPrice %j', async (value) => {
      stubProduct({ variants: [variantFixture({ price: 1250, compareAtPrice: value })] });
      renderApp('/products/dragon');

      expect((await findOffer()).querySelector('s')).toBeNull();
    });

    it('shows "in stock" for an available stock product', async () => {
      stubProduct();
      renderApp('/products/dragon');

      const offer = await findOffer();
      expect(offer).toHaveTextContent('В наличност');
      expect(offer).not.toHaveTextContent('Изчерпан');
    });

    it('shows "sold out" for a stock product that is not available', async () => {
      stubProduct({ variants: [variantFixture({ isAvailable: false })] });
      renderApp('/products/dragon');

      const offer = await findOffer();
      expect(offer).toHaveTextContent('Изчерпан');
      expect(offer).not.toHaveTextContent('В наличност');
    });

    it('shows "made to order" with the lead time in days', async () => {
      stubProduct({ fulfilmentType: 'MadeToOrder', leadTimeDays: 5 });
      renderApp('/products/dragon');

      expect(await findOffer()).toHaveTextContent('По поръчка · 5 дни');
    });

    it('shows "made to order" without a lead time when none is set', async () => {
      stubProduct({ fulfilmentType: 'MadeToOrder', leadTimeDays: null });
      renderApp('/products/dragon');

      const offer = await findOffer();
      expect(offer).toHaveTextContent('По поръчка');
      expect(offer).not.toHaveTextContent('дни');
    });

    it('announces changes politely', async () => {
      stubProduct();
      renderApp('/products/dragon');

      expect(await findOffer()).toHaveAttribute('aria-live', 'polite');
    });
  });

  describe('variants', () => {
    it('shows one variant without a control', async () => {
      stubProduct();
      renderApp('/products/dragon');

      const main = await findMain();
      expect(await within(main).findByText('Small')).toBeInTheDocument();
      expect(within(main).queryByRole('radio')).not.toBeInTheDocument();
      expect(within(main).queryByRole('group')).not.toBeInTheDocument();
    });

    it('offers several variants as radio buttons in a labelled group', async () => {
      stubProduct({ variants: twoVariants });
      renderApp('/products/dragon');

      const group = await screen.findByRole('group', { name: 'Вариант' });
      expect(
        within(group)
          .getAllByRole('radio')
          .map((radio) => radio.getAttribute('value')),
      ).toEqual(['DRG-S', 'DRG-L']);
      expect(within(group).getByRole('radio', { name: 'Small' })).toBeChecked();
    });

    it('selects the first available variant by default', async () => {
      stubProduct({
        variants: [
          variantFixture({
            id: 'v1',
            sku: 'A',
            name: 'Sold out',
            isAvailable: false,
            sortOrder: 1,
          }),
          variantFixture({ id: 'v2', sku: 'B', name: 'Free', price: 2000, sortOrder: 2 }),
        ],
      });
      renderApp('/products/dragon');

      expect(await screen.findByRole('radio', { name: 'Free' })).toBeChecked();
      expect(plain((await findOffer()).textContent)).toContain('20,00 €');
    });

    it('selects the first variant when none is available', async () => {
      stubProduct({
        variants: [
          variantFixture({ id: 'v1', sku: 'A', name: 'One', isAvailable: false, sortOrder: 1 }),
          variantFixture({ id: 'v2', sku: 'B', name: 'Two', isAvailable: false, sortOrder: 2 }),
        ],
      });
      renderApp('/products/dragon');

      expect(await screen.findByRole('radio', { name: /One/ })).toBeChecked();
    });

    it('takes the selected variant from the address', async () => {
      stubProduct({ variants: twoVariants });
      renderApp('/products/dragon?variant=DRG-L');

      expect(await screen.findByRole('radio', { name: 'Large' })).toBeChecked();
      expect(plain((await findOffer()).textContent)).toContain('24,00 €');
    });

    it('uses the default variant for an unknown variant in the address', async () => {
      stubProduct({ variants: twoVariants });
      renderApp('/products/dragon?variant=nope');

      expect(await screen.findByRole('radio', { name: 'Small' })).toBeChecked();
    });

    it('changes the price and the availability, and the address, when another variant is chosen', async () => {
      stubProduct({
        variants: [
          variantFixture({ id: 'v1', sku: 'DRG-S', name: 'Small', price: 1250, sortOrder: 1 }),
          variantFixture({
            id: 'v2',
            sku: 'DRG-L',
            name: 'Large',
            price: 2400,
            isAvailable: false,
            sortOrder: 2,
          }),
        ],
      });
      renderApp('/products/dragon');
      await screen.findByRole('group', { name: 'Вариант' });
      expect(location()).toBe('/products/dragon POP');

      fireEvent.click(screen.getByRole('radio', { name: /Large/ }));

      const offer = await findOffer();
      await waitFor(() => {
        expect(plain(offer.textContent)).toContain('24,00 €');
      });
      expect(offer).toHaveTextContent('Изчерпан');
      expect(screen.getByRole('radio', { name: /Large/ })).toBeChecked();
      expect(location()).toBe('/products/dragon?variant=DRG-L REPLACE');
    });

    it('shows a sold-out variant as such, in words, and lets it be chosen', async () => {
      stubProduct({
        variants: [
          variantFixture({ id: 'v1', sku: 'A', name: 'Small', sortOrder: 1 }),
          variantFixture({ id: 'v2', sku: 'B', name: 'Large', isAvailable: false, sortOrder: 2 }),
        ],
      });
      renderApp('/products/dragon');

      const soldOut = await screen.findByRole('radio', { name: 'Large (Изчерпан)' });
      expect(soldOut).toBeEnabled();
      expect(screen.getByRole('radio', { name: 'Small' })).toBeEnabled();
      fireEvent.click(soldOut);
      expect(soldOut).toBeChecked();
    });

    it('keeps other parts of the address when a variant is chosen', async () => {
      stubProduct({ variants: twoVariants });
      renderApp('/products/dragon?ref=mail');

      fireEvent.click(await screen.findByRole('radio', { name: 'Large' }));

      await waitFor(() => {
        expect(location()).toBe('/products/dragon?ref=mail&variant=DRG-L REPLACE');
      });
    });
  });

  describe('gallery', () => {
    const images = [
      imageFixture({
        id: 'common',
        storageKey: 'demo/common.jpg',
        altText: 'Common',
        sortOrder: 1,
        isPrimary: true,
      }),
      imageFixture({
        id: 'small',
        variantId: 'v1',
        storageKey: 'demo/small.jpg',
        altText: 'Small only',
        sortOrder: 2,
      }),
      imageFixture({
        id: 'large',
        variantId: 'v2',
        storageKey: 'demo/large.jpg',
        altText: 'Large only',
        sortOrder: 3,
      }),
    ];
    const options = { config: { imageBaseUrl: IMAGE_BASE } };

    function thumbnails(): string[] {
      return within(screen.getByRole('region', { name: 'Снимки на продукта' }))
        .getAllByRole('button')
        .map((button) => button.getAttribute('aria-label') ?? '');
    }

    it('shows the primary image large, with alt text, size and the address from the config', async () => {
      stubProduct({ variants: twoVariants, images }, options);
      renderApp('/products/dragon');

      const large = await screen.findByRole('img', { name: 'Common' });
      expect(large).toHaveAttribute('src', 'http://img.test/demo/common.jpg');
      expect(large).toHaveAttribute('width');
      expect(large).toHaveAttribute('height');
      expect(large).not.toHaveAttribute('loading', 'lazy');
    });

    it('shows the common images and those of the selected variant, in order', async () => {
      stubProduct({ variants: twoVariants, images }, options);
      renderApp('/products/dragon');
      await screen.findByRole('img', { name: 'Common' });

      expect(thumbnails()).toEqual(['Common', 'Small only']);

      fireEvent.click(screen.getByRole('radio', { name: 'Large' }));

      await waitFor(() => {
        expect(thumbnails()).toEqual(['Common', 'Large only']);
      });
    });

    it('shows only the common images for a variant without its own', async () => {
      stubProduct(
        {
          variants: [
            ...twoVariants,
            variantFixture({ id: 'v3', sku: 'DRG-XL', name: 'XL', sortOrder: 3 }),
          ],
          images,
        },
        options,
      );
      renderApp('/products/dragon?variant=DRG-XL');

      await screen.findByRole('img', { name: 'Common' });
      expect(screen.queryByRole('button', { name: 'Small only' })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Large only' })).not.toBeInTheDocument();
    });

    it('shows the first shown image large when the primary one belongs to another variant', async () => {
      stubProduct(
        {
          variants: twoVariants,
          images: [
            imageFixture({
              id: 'a',
              variantId: 'v2',
              storageKey: 'demo/a.jpg',
              altText: 'A',
              sortOrder: 1,
              isPrimary: true,
            }),
            imageFixture({ id: 'b', storageKey: 'demo/b.jpg', altText: 'B', sortOrder: 2 }),
          ],
        },
        options,
      );
      renderApp('/products/dragon');

      expect(await screen.findByRole('img', { name: 'B' })).toHaveAttribute(
        'src',
        'http://img.test/demo/b.jpg',
      );
    });

    it('shows the chosen thumbnail large, marks it as pressed, and loads thumbnails lazily', async () => {
      stubProduct({ variants: twoVariants, images }, options);
      renderApp('/products/dragon');
      await screen.findByRole('img', { name: 'Common' });

      const small = screen.getByRole('button', { name: 'Small only' });
      expect(screen.getByRole('button', { name: 'Common' })).toHaveAttribute(
        'aria-pressed',
        'true',
      );
      expect(small).toHaveAttribute('aria-pressed', 'false');
      expect(small.querySelector('img')).toHaveAttribute('loading', 'lazy');

      fireEvent.click(small);

      expect(await screen.findByRole('img', { name: 'Small only' })).toHaveAttribute(
        'src',
        'http://img.test/demo/small.jpg',
      );
      expect(small).toHaveAttribute('aria-pressed', 'true');
      expect(screen.getByRole('button', { name: 'Common' })).toHaveAttribute(
        'aria-pressed',
        'false',
      );
    });

    it('has thumbnails that can be reached with the keyboard', async () => {
      stubProduct({ variants: twoVariants, images }, options);
      renderApp('/products/dragon');
      await screen.findByRole('img', { name: 'Common' });

      for (const button of within(
        screen.getByRole('region', { name: 'Снимки на продукта' }),
      ).getAllByRole('button')) {
        expect(button.tagName).toBe('BUTTON');
        expect(button).not.toHaveAttribute('tabindex', '-1');
      }
    });

    it('shows no thumbnails for a single image', async () => {
      stubProduct({ images: [images[0]] }, options);
      renderApp('/products/dragon');

      expect(await screen.findByRole('img', { name: 'Common' })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Common' })).not.toBeInTheDocument();
    });

    it('names a thumbnail by its number when the image has no alt text', async () => {
      stubProduct(
        {
          images: [
            imageFixture({ id: 'a', altText: '', sortOrder: 1, isPrimary: true }),
            imageFixture({ id: 'b', altText: '', sortOrder: 2 }),
          ],
        },
        options,
      );
      renderApp('/products/dragon');

      expect(await screen.findByRole('button', { name: 'Снимка 2' })).toBeInTheDocument();
    });

    it('shows the placeholder, not a broken image, for a product without images', async () => {
      stubProduct({ images: [] }, options);
      renderApp('/products/dragon');

      expect(await screen.findByTestId('image-placeholder')).toBeInTheDocument();
      expect(screen.queryByRole('img')).not.toBeInTheDocument();
      expect(screen.getByRole('heading', { level: 1, name: 'Dragon' })).toBeInTheDocument();
    });

    it('shows the placeholder when the environment has no image address', async () => {
      stubProduct({ images });
      renderApp('/products/dragon');

      expect((await screen.findAllByTestId('image-placeholder')).length).toBeGreaterThan(0);
      expect(document.querySelector('img')).toBeNull();
    });

    it('swaps an image that fails to load for the placeholder', async () => {
      stubProduct({ images: [images[0]] }, options);
      renderApp('/products/dragon');

      fireEvent.error(await screen.findByRole('img', { name: 'Common' }));

      expect(await screen.findByTestId('image-placeholder')).toBeInTheDocument();
      expect(screen.queryByRole('img')).not.toBeInTheDocument();
    });
  });
});
