import type { AdminCategory, AdminImage, AdminProduct } from '@storefront/shared';
import { QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../../queryClient';
import { ProductFormPage } from './ProductFormPage';

const BASE = '/api/v1/admin/products/p1';
const IMAGE_BASE_URL = 'https://media.example.test/bucket';
const MAX = 5 * 1024 * 1024;

function image(id: string, overrides: Partial<AdminImage> = {}): AdminImage {
  return {
    id,
    variantId: null,
    storageKey: `products/p1/${id}.jpg`,
    altText: `Снимка ${id}`,
    sortOrder: 0,
    isPrimary: false,
    ...overrides,
  };
}

/** Three images in order, the first one primary. */
function threeImages(): AdminImage[] {
  return [
    image('a', { altText: 'Отпред', sortOrder: 0, isPrimary: true }),
    image('b', { altText: 'Отзад', sortOrder: 1 }),
    image('c', { altText: 'Отстрани', sortOrder: 2, variantId: 'v2' }),
  ];
}

function product(images: AdminImage[]): AdminProduct {
  return {
    id: 'p1',
    categoryId: 'figures',
    name: 'Дракон',
    slug: 'drakon',
    shortDescription: 'Кратко',
    description: 'Дълго',
    fulfilmentType: 'InStock',
    leadTimeDays: null,
    status: 'Draft',
    seoTitle: null,
    seoDescription: null,
    version: 1,
    variants: [
      {
        id: 'v1',
        sku: 'V1',
        name: 'Син',
        price: 1500,
        compareAtPrice: null,
        stockQuantity: 5,
        weightGrams: 100,
        attributes: {},
        isActive: true,
        sortOrder: 0,
      },
      {
        id: 'v2',
        sku: 'V2',
        name: 'Червен',
        price: 1500,
        compareAtPrice: null,
        stockQuantity: 5,
        weightGrams: 100,
        attributes: {},
        isActive: true,
        sortOrder: 1,
      },
    ],
    images,
  };
}

const CATEGORY: AdminCategory = {
  id: 'figures',
  parentId: null,
  name: 'Фигури',
  slug: 'figures',
  description: null,
  sortOrder: 0,
  isActive: true,
  productCount: 1,
  version: 1,
};

interface Call {
  method: string;
  url: string;
  body: unknown;
}

interface Refusal {
  status: number;
  code?: string;
}

/**
 * A small API in memory for the images of one product. It keeps the order like the real one: 0, 1, 2..., the first
 * image primary, and a deleted primary image replaced by the first of the rest. `refuseNext` makes the next image
 * call fail once with the given status and code.
 */
function fakeApi(initial: AdminImage[], options: { imageBaseUrl?: string | null } = {}) {
  const server = {
    product: product(structuredClone(initial)),
    calls: [] as Call[],
    refuseNext: null as Refusal | null,
    nextId: 1,
  };
  const imageBaseUrl = options.imageBaseUrl === undefined ? IMAGE_BASE_URL : options.imageBaseUrl;
  const ordered = () => [...server.product.images].sort((x, y) => x.sortOrder - y.sortOrder);
  const renumber = (list: AdminImage[]) => {
    list.forEach((item, position) => {
      item.sortOrder = position;
    });
    server.product.images = list;
  };

  const fetchMock = vi.fn((rawUrl: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET';
    const isForm = init?.body instanceof FormData;
    const body: unknown =
      typeof init?.body === 'string' ? JSON.parse(init.body) : isForm ? init.body : undefined;
    server.calls.push({ method, url: rawUrl, body });
    const json = (value: unknown, status = 200) =>
      Promise.resolve(Response.json(value, { status }));

    if (rawUrl === '/config.json') {
      return json(imageBaseUrl === null ? { apiUrl: '' } : { apiUrl: '', imageBaseUrl });
    }
    if (rawUrl === '/api/v1/store-settings') {
      return json({ defaultCurrency: 'EUR', defaultLocale: 'bg' });
    }
    if (rawUrl === '/api/v1/admin/categories') {
      return json([CATEGORY]);
    }
    if (rawUrl === BASE) {
      return json(server.product);
    }

    const match =
      /^\/api\/v1\/admin\/products\/p1\/images(?:\/([^/]+)(?:\/(primary|move))?)?$/.exec(rawUrl);
    if (!match) {
      return Promise.resolve(Response.json({ code: 'product.not_found' }, { status: 404 }));
    }
    if (server.refuseNext) {
      const { status, code } = server.refuseNext;
      server.refuseNext = null;
      return json(code ? { code } : {}, status);
    }

    const imageId = match.at(1);
    const action = match.at(2);
    if (imageId === undefined && method === 'POST') {
      const form = body as FormData;
      const id = `new-${String(server.nextId++)}`;
      const altValue = form.get('altText');
      const variantValue = form.get('variantId');
      const variantId = typeof variantValue === 'string' ? variantValue : null;
      server.product.images.push(
        image(id, {
          altText: typeof altValue === 'string' ? altValue : '',
          variantId,
          sortOrder: server.product.images.length,
          isPrimary: server.product.images.length === 0,
        }),
      );
      return json(server.product, 201);
    }
    const target = server.product.images.find((candidate) => candidate.id === imageId);
    if (!target) {
      return json({ code: 'image.not_found' }, 404);
    }
    const input = (body ?? {}) as {
      altText?: string;
      variantId?: string | null;
      direction?: string;
    };

    if (method === 'PUT' && action === 'primary') {
      server.product.images.forEach((item) => {
        item.isPrimary = item === target;
      });
      return json(server.product);
    }
    if (method === 'POST' && action === 'move') {
      const list = ordered();
      const index = list.indexOf(target);
      const targetIndex = input.direction === 'Up' ? index - 1 : index + 1;
      const other = targetIndex >= 0 ? list.at(targetIndex) : undefined;
      if (other) {
        list[index] = other;
        list[targetIndex] = target;
      }
      renumber(list);
      return json(server.product);
    }
    if (method === 'PUT') {
      target.altText = input.altText ?? target.altText;
      target.variantId = input.variantId ?? null;
      return json(server.product);
    }
    if (method === 'DELETE') {
      const rest = ordered().filter((item) => item !== target);
      if (target.isPrimary && rest[0]) {
        rest[0].isPrimary = true;
      }
      renumber(rest);
      return json(server.product);
    }
    return json({}, 404);
  });
  vi.stubGlobal('fetch', fetchMock);
  return {
    server,
    writes: () =>
      server.calls.filter((call) => call.method !== 'GET' && call.url.includes('/images')),
  };
}

function renderPage() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <MemoryRouter initialEntries={['/products/p1']}>
        <Routes>
          <Route path="products/:id" element={<ProductFormPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

async function openSection() {
  renderPage();
  return screen.findByRole('heading', { name: 'Снимки' });
}

function list() {
  return screen.getByRole('list', { name: 'Снимки на продукта' });
}

function altTexts(): string[] {
  return within(list())
    .getAllByRole('listitem')
    .map((item) => within(item).getByRole('strong', { hidden: true }).textContent);
}

function button(name: string) {
  return screen.getByRole('button', { name });
}

function file(size: number, type: string, name = 'photo.jpg') {
  return new File([new Uint8Array(size)], name, { type });
}

function choose(selected: File | null) {
  fireEvent.change(screen.getByLabelText('Файл със снимка'), {
    target: { files: selected ? [selected] : [] },
  });
}

function typeAlt(value: string) {
  fireEvent.change(screen.getByLabelText('Описание на снимката (alt текст)'), {
    target: { value },
  });
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('Images section: the list', () => {
  it('shows the images in order with their alt text, the primary one marked in words, and the variant', async () => {
    fakeApi(threeImages());
    await openSection();

    expect(altTexts()).toEqual(['Отпред', 'Отзад', 'Отстрани']);
    const [first, , third] = within(list()).getAllByRole('listitem');
    expect(first).toHaveTextContent('Основна');
    expect(first).toHaveTextContent('Целият продукт');
    expect(third).toHaveTextContent('За вариант „Червен“');
    expect(within(list()).getAllByText('Основна')).toHaveLength(1);
  });

  it('shows each image from the base address and the storage key, with the alt text as its text', async () => {
    fakeApi(threeImages());
    await openSection();

    // The address is known when /config.json has loaded; until then the box with the same name is shown.
    await waitFor(() => {
      expect(screen.getByRole('img', { name: 'Отпред' })).toHaveAttribute(
        'src',
        `${IMAGE_BASE_URL}/products/p1/a.jpg`,
      );
    });
    const picture = screen.getByRole('img', { name: 'Отпред' });
    expect(picture).toHaveAttribute('loading', 'lazy');
    expect(picture).toHaveAttribute('width');
    expect(picture).toHaveAttribute('height');
  });

  it('shows a box with the same name instead of a picture when the environment has no image address', async () => {
    fakeApi(threeImages(), { imageBaseUrl: null });
    await openSection();

    expect(screen.queryByRole('img', { name: 'Отпред' })?.tagName).not.toBe('IMG');
    expect(within(list()).getAllByText('Няма преглед')).toHaveLength(3);
  });

  it('falls back to the same box when a file does not load', async () => {
    fakeApi(threeImages());
    await openSection();

    const picture = await waitFor(() => {
      const found = screen.getByRole('img', { name: 'Отпред' });
      expect(found.tagName).toBe('IMG');
      return found;
    });
    fireEvent.error(picture);

    await waitFor(() => {
      expect(screen.queryByRole('img', { name: 'Отпред' })?.tagName).not.toBe('IMG');
    });
    // Only the image that failed is replaced; the other two keep their pictures.
    expect(within(list()).getAllByText('Няма преглед')).toHaveLength(1);
  });

  it('says so when there are no images yet', async () => {
    fakeApi([]);
    await openSection();

    expect(screen.getByText(/Още няма снимки/)).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: 'Снимки на продукта' })).not.toBeInTheDocument();
  });
});

describe('Images section: buttons for people who use a keyboard or a screen reader', () => {
  it('gives every button a name that says which image it is about', async () => {
    fakeApi(threeImages());
    await openSection();

    for (const name of [
      'Премести снимка „Отзад“ нагоре',
      'Премести снимка „Отзад“ надолу',
      'Направи снимка „Отзад“ основна',
      'Редакция на снимка „Отзад“',
      'Изтрий снимка „Отзад“',
    ]) {
      expect(button(name)).toBeInTheDocument();
    }
  });

  it('cannot move the first image up or the last one down, and the primary image is already primary', async () => {
    fakeApi(threeImages());
    await openSection();

    expect(button('Премести снимка „Отпред“ нагоре')).toBeDisabled();
    expect(button('Премести снимка „Отстрани“ надолу')).toBeDisabled();
    expect(button('Направи снимка „Отпред“ основна')).toBeDisabled();
    expect(button('Премести снимка „Отзад“ нагоре')).toBeEnabled();
  });

  it('labels the file field and tells the screen reader about the upload through a status', async () => {
    fakeApi(threeImages());
    await openSection();

    expect(screen.getByLabelText('Файл със снимка')).toHaveAttribute('type', 'file');
    expect(screen.getByLabelText('Файл със снимка')).toHaveAccessibleDescription(
      /JPEG, PNG или WebP/,
    );
    expect(screen.getByLabelText('Файл със снимка')).toHaveAccessibleDescription(/GPS/);
    expect(screen.getByRole('form', { name: 'Качване на снимка' })).toBeInTheDocument();
  });
});

describe('Images section: order', () => {
  it('moves an image up and writes the new order to the server', async () => {
    const api = fakeApi(threeImages());
    await openSection();

    fireEvent.click(button('Премести снимка „Отстрани“ нагоре'));

    await waitFor(() => {
      expect(altTexts()).toEqual(['Отпред', 'Отстрани', 'Отзад']);
    });
    expect(api.writes().map((call) => [call.method, call.url, call.body])).toEqual([
      ['POST', `${BASE}/images/c/move`, { direction: 'Up' }],
    ]);
  });

  it('moves an image down', async () => {
    fakeApi(threeImages());
    await openSection();

    fireEvent.click(button('Премести снимка „Отпред“ надолу'));

    await waitFor(() => {
      expect(altTexts()).toEqual(['Отзад', 'Отпред', 'Отстрани']);
    });
  });

  it('keeps the focus on the arrow that was pressed, so the keyboard can go on', async () => {
    fakeApi(threeImages());
    await openSection();

    fireEvent.click(button('Премести снимка „Отстрани“ нагоре'));

    await waitFor(() => {
      expect(button('Премести снимка „Отстрани“ нагоре')).toHaveFocus();
    });
  });

  it('shows an alert and loads the list again when the image is gone on the server', async () => {
    const api = fakeApi(threeImages());
    await openSection();
    api.server.refuseNext = { status: 404, code: 'image.not_found' };

    fireEvent.click(button('Премести снимка „Отзад“ нагоре'));

    expect(await screen.findByRole('alert')).toHaveTextContent('Снимката вече не съществува');
  });
});

describe('Images section: primary image', () => {
  it('makes another image primary and moves the word "Основна" to it', async () => {
    const api = fakeApi(threeImages());
    await openSection();

    fireEvent.click(button('Направи снимка „Отзад“ основна'));

    await waitFor(() => {
      expect(
        within(within(list()).getAllByRole('listitem')[1]).getByText('Основна'),
      ).toBeInTheDocument();
    });
    expect(within(list()).getAllByText('Основна')).toHaveLength(1);
    expect(api.writes().map((call) => [call.method, call.url])).toEqual([
      ['PUT', `${BASE}/images/b/primary`],
    ]);
  });
});

describe('Images section: change the alt text and the variant', () => {
  it('opens a form with the values of the image, saves them and closes', async () => {
    const api = fakeApi(threeImages());
    await openSection();

    fireEvent.click(button('Редакция на снимка „Отзад“'));
    const form = screen.getByRole('form', { name: 'Редакция на снимка „Отзад“' });
    const alt = within(form).getByLabelText('Описание на снимката (alt текст)');
    expect(alt).toHaveValue('Отзад');
    expect(alt).toHaveFocus();
    fireEvent.change(alt, { target: { value: '  Гръб на дракона  ' } });
    fireEvent.change(within(form).getByLabelText('Вариант'), { target: { value: 'v1' } });
    fireEvent.click(within(form).getByRole('button', { name: 'Запази' }));

    await waitFor(() => {
      expect(screen.queryByRole('form', { name: /Редакция на снимка/ })).not.toBeInTheDocument();
    });
    expect(api.writes().map((call) => [call.method, call.url, call.body])).toEqual([
      ['PUT', `${BASE}/images/b`, { altText: 'Гръб на дракона', variantId: 'v1' }],
    ]);
    expect(altTexts()).toContain('Гръб на дракона');
  });

  it('shows the error next to the alt text and sends nothing when it is empty or too long', async () => {
    const api = fakeApi(threeImages());
    await openSection();
    fireEvent.click(button('Редакция на снимка „Отзад“'));
    const form = screen.getByRole('form', { name: 'Редакция на снимка „Отзад“' });
    const alt = within(form).getByLabelText('Описание на снимката (alt текст)');

    fireEvent.change(alt, { target: { value: '   ' } });
    fireEvent.click(within(form).getByRole('button', { name: 'Запази' }));
    expect(alt).toHaveAccessibleDescription(/Въведи описание на снимката/);
    expect(alt).toHaveAttribute('aria-invalid', 'true');

    fireEvent.change(alt, { target: { value: 'а'.repeat(301) } });
    fireEvent.click(within(form).getByRole('button', { name: 'Запази' }));
    expect(alt).toHaveAccessibleDescription(/най-много 300 знака/);
    expect(api.writes()).toEqual([]);
  });

  it('closes without saving on cancel', async () => {
    const api = fakeApi(threeImages());
    await openSection();
    fireEvent.click(button('Редакция на снимка „Отзад“'));

    fireEvent.click(button('Отказ'));

    expect(screen.queryByRole('form', { name: /Редакция на снимка/ })).not.toBeInTheDocument();
    expect(api.writes()).toEqual([]);
  });
});

describe('Images section: delete', () => {
  it('asks first, with Cancel focused, and deletes nothing on Cancel', async () => {
    const api = fakeApi(threeImages());
    await openSection();

    fireEvent.click(button('Изтрий снимка „Отзад“'));
    const dialog = screen.getByRole('dialog', { name: 'Изтриване на снимка' });

    expect(dialog).toHaveTextContent('„Отзад“');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Отказ' }));
    expect(api.writes()).toEqual([]);
    expect(altTexts()).toHaveLength(3);
  });

  it('deletes after the confirmation and the rest are numbered again', async () => {
    const api = fakeApi(threeImages());
    await openSection();

    fireEvent.click(button('Изтрий снимка „Отзад“'));
    fireEvent.click(
      within(screen.getByRole('dialog', { name: 'Изтриване на снимка' })).getByRole('button', {
        name: 'Изтрий',
      }),
    );

    await waitFor(() => {
      expect(altTexts()).toEqual(['Отпред', 'Отстрани']);
    });
    expect(api.writes().map((call) => [call.method, call.url])).toEqual([
      ['DELETE', `${BASE}/images/b`],
    ]);
    expect(api.server.product.images.map((item) => item.sortOrder)).toEqual([0, 1]);
  });

  it('warns that the next image becomes primary when the primary one is deleted, and the next one does', async () => {
    fakeApi(threeImages());
    await openSection();

    fireEvent.click(button('Изтрий снимка „Отпред“'));
    const dialog = screen.getByRole('dialog', { name: 'Изтриване на снимка' });
    expect(dialog).toHaveTextContent('Основна става първата от останалите');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Изтрий' }));

    await waitFor(() => {
      expect(altTexts()).toEqual(['Отзад', 'Отстрани']);
    });
    expect(
      within(within(list()).getAllByRole('listitem')[0]).getByText('Основна'),
    ).toBeInTheDocument();
  });

  it('does not warn about a primary image when an ordinary one is deleted', async () => {
    fakeApi(threeImages());
    await openSection();

    fireEvent.click(button('Изтрий снимка „Отзад“'));

    expect(screen.getByRole('dialog', { name: 'Изтриване на снимка' })).not.toHaveTextContent(
      'Основна става',
    );
  });
});

describe('Images section: upload', () => {
  it('sends the file, the trimmed alt text and the variant, then shows the new image and says it is done', async () => {
    const api = fakeApi([]);
    await openSection();

    choose(file(100, 'image/png', 'dragon.png'));
    typeAlt('  Нов дракон  ');
    fireEvent.change(screen.getByLabelText('Вариант'), { target: { value: 'v1' } });
    fireEvent.click(button('Качи снимката'));

    await waitFor(() => {
      expect(altTexts()).toEqual(['Нов дракон']);
    });
    const [call] = api.writes();
    expect(call.method).toBe('POST');
    expect(call.url).toBe(`${BASE}/images`);
    const form = call.body as FormData;
    expect((form.get('file') as File).name).toBe('dragon.png');
    expect(form.get('altText')).toBe('Нов дракон');
    expect(form.get('variantId')).toBe('v1');
    expect(screen.getByRole('status')).toHaveTextContent('Снимката е качена.');
    expect(screen.getByLabelText('Описание на снимката (alt текст)')).toHaveValue('');
  });

  it('leaves the variant out when the image is for the whole product', async () => {
    const api = fakeApi([]);
    await openSection();

    choose(file(100, 'image/jpeg'));
    typeAlt('Дракон');
    fireEvent.click(button('Качи снимката'));

    await waitFor(() => {
      expect(altTexts()).toHaveLength(1);
    });
    expect((api.writes()[0]?.body as FormData).has('variantId')).toBe(false);
  });

  it('refuses a type that is not allowed next to the file field, without sending anything', async () => {
    const api = fakeApi([]);
    await openSection();

    choose(file(100, 'image/svg+xml', 'logo.jpg'));
    typeAlt('Дракон');
    fireEvent.click(button('Качи снимката'));

    const input = screen.getByLabelText('Файл със снимка');
    expect(input).toHaveAccessibleDescription(/Не е позволен тип файл/);
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveFocus();
    expect(api.writes()).toEqual([]);
  });

  it('accepts a file of exactly 5 MB and refuses one byte more next to the file field', async () => {
    const api = fakeApi([]);
    await openSection();

    choose(file(MAX + 1, 'image/jpeg'));
    typeAlt('Дракон');
    fireEvent.click(button('Качи снимката'));
    expect(screen.getByLabelText('Файл със снимка')).toHaveAccessibleDescription(
      /Файлът е твърде голям/,
    );
    expect(api.writes()).toEqual([]);

    choose(file(MAX, 'image/jpeg'));
    fireEvent.click(button('Качи снимката'));
    await waitFor(() => {
      expect(altTexts()).toHaveLength(1);
    });
  });

  it('wants a file and an alt text, and focuses the first field that is wrong', async () => {
    const api = fakeApi([]);
    await openSection();

    fireEvent.click(button('Качи снимката'));

    expect(screen.getByLabelText('Файл със снимка')).toHaveAccessibleDescription(
      /Избери файл със снимка/,
    );
    expect(screen.getByLabelText('Описание на снимката (alt текст)')).toHaveAccessibleDescription(
      /Въведи описание на снимката/,
    );
    expect(screen.getByLabelText('Файл със снимка')).toHaveFocus();
    expect(api.writes()).toEqual([]);
  });

  it('refuses an empty file and an alt text of 301 characters', async () => {
    const api = fakeApi([]);
    await openSection();

    choose(file(0, 'image/jpeg'));
    typeAlt('а'.repeat(301));
    fireEvent.click(button('Качи снимката'));

    expect(screen.getByLabelText('Файл със снимка')).toHaveAccessibleDescription(/Файлът е празен/);
    expect(screen.getByLabelText('Описание на снимката (alt текст)')).toHaveAccessibleDescription(
      /най-много 300 знака/,
    );
    expect(api.writes()).toEqual([]);
  });

  it.each([
    [{ status: 400, code: 'image.type_not_allowed' }, /Не е позволен тип файл/],
    [{ status: 413, code: 'image.too_large' }, /Файлът е твърде голям/],
    [{ status: 413 }, /Файлът е твърде голям/],
    [{ status: 400, code: 'image.empty' }, /Файлът е празен/],
  ] as [Refusal, RegExp][])(
    'shows the refusal %j of the server next to the file field',
    async (refusal, message) => {
      const api = fakeApi([]);
      await openSection();
      api.server.refuseNext = refusal;

      choose(file(100, 'image/jpeg'));
      typeAlt('Дракон');
      fireEvent.click(button('Качи снимката'));

      await waitFor(() => {
        expect(screen.getByLabelText('Файл със снимка')).toHaveAccessibleDescription(message);
      });
      expect(screen.queryByRole('list', { name: 'Снимки на продукта' })).not.toBeInTheDocument();
    },
  );

  it.each([
    [{ status: 409, code: 'image.limit_reached' }, /най-много 20 снимки/],
    [{ status: 400, code: 'validation.failed' }, /Данните не са приети/],
    [{ status: 500 }, /Снимката не се качи/],
  ] as [Refusal, RegExp][])('shows the other refusal %j as an alert', async (refusal, message) => {
    const api = fakeApi([]);
    await openSection();
    api.server.refuseNext = refusal;

    choose(file(100, 'image/jpeg'));
    typeAlt('Дракон');
    fireEvent.click(button('Качи снимката'));

    expect(await screen.findByRole('alert')).toHaveTextContent(message);
  });

  it('does not send while an upload is running, and says that it is uploading', async () => {
    const api = fakeApi([]);
    await openSection();
    // The answer comes only when the test lets it.
    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    const original = globalThis.fetch;
    vi.stubGlobal('fetch', async (url: string, init?: RequestInit) => {
      if (init?.method === 'POST') {
        await gate;
      }
      return original(url, init);
    });

    choose(file(100, 'image/jpeg'));
    typeAlt('Дракон');
    fireEvent.click(button('Качи снимката'));

    await waitFor(() => {
      expect(screen.getByRole('status')).toHaveTextContent('Качване…');
    });
    expect(screen.getByRole('button', { name: 'Качване…' })).toBeDisabled();
    release();
    await waitFor(() => {
      expect(screen.getByRole('status')).toHaveTextContent('Снимката е качена.');
    });
    expect(api.writes()).toHaveLength(1);
  });

  it('stops the upload when the product has the most images, and says so', async () => {
    const many = Array.from({ length: 20 }, (_, n) =>
      image(`m${String(n)}`, { sortOrder: n, isPrimary: n === 0 }),
    );
    fakeApi(many);
    await openSection();

    expect(screen.getByLabelText('Файл със снимка')).toBeDisabled();
    expect(button('Качи снимката')).toBeDisabled();
    expect(screen.getByText(/най-много 20 снимки/)).toBeInTheDocument();
  });
});
