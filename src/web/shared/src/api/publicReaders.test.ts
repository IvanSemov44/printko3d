import { describe, expect, it } from 'vitest';
import {
  readCategories,
  readProductDetails,
  readProductPage,
  readStoreSettings,
} from './publicReaders';

const settings = {
  storeName: 'Shop',
  defaultCurrency: 'EUR',
  defaultLocale: 'bg',
  contactEmail: 'shop@example.test',
  contactPhone: '+359 111 222 333',
  logoKey: null,
  primaryColor: '#2563EB',
  isDemo: false,
};

const category = {
  id: 'c1',
  parentId: null,
  name: 'Puzzles',
  slug: 'puzzles',
  description: null,
  imageKey: null,
  sortOrder: 1,
};

const card = {
  id: 'p1',
  name: 'Dragon',
  slug: 'dragon',
  shortDescription: 'Short',
  fulfilmentType: 'InStock',
  leadTimeDays: null,
  minPrice: 1250,
  primaryImage: null,
  isAvailable: true,
};

const page = { items: [card], page: 1, pageSize: 12, totalCount: 1, totalPages: 1 };

const variant = {
  id: 'v1',
  sku: 'DRG-S',
  name: 'Small',
  price: 1250,
  compareAtPrice: null,
  isAvailable: true,
  weightGrams: 40,
  attributes: { size: 'S' },
  sortOrder: 1,
};

const image = {
  id: 'i1',
  variantId: null,
  storageKey: 'demo/dragon.jpg',
  altText: 'A dragon',
  sortOrder: 1,
  isPrimary: true,
};

const product = {
  id: 'p1',
  name: 'Dragon',
  slug: 'dragon',
  shortDescription: 'Short',
  description: 'Long',
  fulfilmentType: 'MadeToOrder',
  leadTimeDays: 5,
  seoTitle: '',
  seoDescription: '',
  isAvailable: true,
  variants: [variant],
  images: [image],
};

function without(object: Record<string, unknown>, key: string) {
  return Object.fromEntries(Object.entries(object).filter(([name]) => name !== key));
}

describe('readStoreSettings', () => {
  it('reads every field', () => {
    expect(readStoreSettings(settings)).toEqual(settings);
  });

  it.each(Object.keys(settings))('names the field %s when it is missing', (key) => {
    expect(() => readStoreSettings(without(settings, key))).toThrow(
      `storeSettings.${key} should be`,
    );
  });

  it('rejects a field of the wrong type', () => {
    expect(() => readStoreSettings({ ...settings, isDemo: 'yes' })).toThrow(
      'storeSettings.isDemo should be a boolean',
    );
  });

  it('rejects a body that is not an object', () => {
    expect(() => readStoreSettings([])).toThrow('storeSettings should be an object');
  });
});

describe('readCategories', () => {
  it('reads the list and turns the sort order into a number', () => {
    expect(readCategories([{ ...category, sortOrder: '2' }])).toEqual([
      { ...category, sortOrder: 2 },
    ]);
  });

  it('names the item and the field that is missing', () => {
    expect(() => readCategories([category, without(category, 'slug')])).toThrow(
      'categories[1].slug should be a string',
    );
  });

  it('rejects a body that is not a list', () => {
    expect(() => readCategories({})).toThrow('categories should be an array');
  });
});

describe('readProductPage', () => {
  it('reads a page and builds the address of the main image', () => {
    const result = readProductPage(
      {
        ...page,
        items: [{ ...card, primaryImage: { storageKey: 'a b.jpg', altText: 'Alt' } }],
      },
      'http://img.test',
    );

    expect(result.items[0]?.primaryImage).toEqual({
      url: 'http://img.test/a%20b.jpg',
      altText: 'Alt',
    });
  });

  it('turns integers sent as strings into numbers', () => {
    const result = readProductPage(
      {
        items: [{ ...card, minPrice: '1250', leadTimeDays: '5' }],
        page: '1',
        pageSize: '12',
        totalCount: '1',
        totalPages: '1',
      },
      null,
    );

    expect(result).toMatchObject({ page: 1, pageSize: 12, totalCount: 1, totalPages: 1 });
    expect(result.items[0]).toMatchObject({ minPrice: 1250, leadTimeDays: 5 });
  });

  it('works out the page count when the answer has none', () => {
    expect(
      readProductPage(without({ ...page, totalCount: 25 }, 'totalPages'), null).totalPages,
    ).toBe(3);
  });

  it.each(['minPrice', 'leadTimeDays', 'isAvailable', 'fulfilmentType', 'slug'])(
    'names the card field %s when it is missing',
    (key) => {
      expect(() => readProductPage({ ...page, items: [without(card, key)] }, null)).toThrow(
        `productPage.items[0].${key} should be`,
      );
    },
  );

  it('rejects a fulfilment type the shop does not know', () => {
    expect(() =>
      readProductPage({ ...page, items: [{ ...card, fulfilmentType: 'Dropship' }] }, null),
    ).toThrow('productPage.items[0].fulfilmentType should be one of InStock, MadeToOrder');
  });

  it('rejects a missing page size', () => {
    expect(() => readProductPage(without(page, 'pageSize'), null)).toThrow(
      'productPage.pageSize should be an integer',
    );
  });

  it('keeps failing on a price that is not a whole number', () => {
    expect(() =>
      readProductPage({ ...page, items: [{ ...card, minPrice: '12.5' }] }, null),
    ).toThrow(RangeError);
  });
});

describe('readProductDetails', () => {
  it('reads the product and orders variants and images by sort order', () => {
    const result = readProductDetails(
      {
        ...product,
        variants: [variant, { ...variant, id: 'v0', sku: 'DRG-XS', sortOrder: 0 }],
        images: [image, { ...image, id: 'i0', sortOrder: 0 }],
      },
      'http://img.test',
    );

    expect(result.variants.map((v) => v.sku)).toEqual(['DRG-XS', 'DRG-S']);
    expect(result.images.map((i) => i.id)).toEqual(['i0', 'i1']);
    expect(result.images[1]?.url).toBe('http://img.test/demo/dragon.jpg');
  });

  it('turns integers sent as strings into numbers and keeps the old price empty', () => {
    const result = readProductDetails(
      {
        ...product,
        leadTimeDays: '5',
        variants: [{ ...variant, price: '1250', weightGrams: '40', sortOrder: '2' }],
        images: [{ ...image, sortOrder: '1' }],
      },
      null,
    );

    expect(result.leadTimeDays).toBe(5);
    expect(result.variants[0]).toMatchObject({
      price: 1250,
      weightGrams: 40,
      sortOrder: 2,
      compareAtPrice: null,
    });
    expect(result.images[0]?.url).toBeNull();
  });

  it('names a variant field that is missing', () => {
    expect(() =>
      readProductDetails({ ...product, variants: [without(variant, 'price')] }, null),
    ).toThrow('product.variants[0].price should be an integer');
  });

  it('names an image field that is missing', () => {
    expect(() =>
      readProductDetails({ ...product, images: [without(image, 'storageKey')] }, null),
    ).toThrow('product.images[0].storageKey should be a string');
  });

  it('rejects attributes that are not text', () => {
    expect(() =>
      readProductDetails({ ...product, variants: [{ ...variant, attributes: { size: 3 } }] }, null),
    ).toThrow('product.variants[0].attributes.size should be a string');
  });

  it.each(['description', 'seoTitle', 'variants', 'images', 'isAvailable'])(
    'names the field %s when it is missing',
    (key) => {
      expect(() => readProductDetails(without(product, key), null)).toThrow(
        `product.${key} should be`,
      );
    },
  );
});
