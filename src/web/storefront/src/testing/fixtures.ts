export function settingsFixture(overrides: Record<string, unknown> = {}) {
  return {
    storeName: 'Test Shop',
    defaultCurrency: 'EUR',
    defaultLocale: 'bg',
    contactEmail: 'shop@example.test',
    contactPhone: '+359 111 222 333',
    logoKey: null,
    primaryColor: '#2563EB',
    isDemo: false,
    ...overrides,
  };
}

export function categoriesFixture() {
  return [
    {
      id: '1',
      parentId: null,
      name: 'Puzzles',
      slug: 'puzzles',
      description: 'Wooden and printed puzzles',
      imageKey: null,
      sortOrder: 1,
    },
    {
      id: '2',
      parentId: null,
      name: 'Figurines',
      slug: 'figurines',
      description: null,
      imageKey: null,
      sortOrder: 2,
    },
    {
      id: '3',
      parentId: '1',
      name: 'Small puzzles',
      slug: 'small-puzzles',
      description: null,
      imageKey: null,
      sortOrder: 1,
    },
  ];
}

export function productFixture(overrides: Record<string, unknown> = {}) {
  return {
    id: '10',
    name: 'Dragon',
    slug: 'dragon',
    shortDescription: 'A printed dragon',
    fulfilmentType: 'InStock',
    leadTimeDays: null,
    minPrice: 1250,
    primaryImage: null,
    isAvailable: true,
    ...overrides,
  };
}

export function variantFixture(overrides: Record<string, unknown> = {}) {
  return {
    id: 'v1',
    sku: 'DRG-S',
    name: 'Small',
    price: 1250,
    compareAtPrice: null,
    isAvailable: true,
    weightGrams: 40,
    attributes: {},
    sortOrder: 1,
    ...overrides,
  };
}

export function imageFixture(overrides: Record<string, unknown> = {}) {
  return {
    id: 'img1',
    variantId: null,
    storageKey: 'demo/dragon-1.jpg',
    altText: 'Dragon front',
    sortOrder: 1,
    isPrimary: false,
    ...overrides,
  };
}

export function productDetailsFixture(overrides: Record<string, unknown> = {}) {
  return {
    id: '10',
    name: 'Dragon',
    slug: 'dragon',
    shortDescription: 'A printed dragon',
    description: 'Printed in PLA. Painted by hand.',
    fulfilmentType: 'InStock',
    leadTimeDays: null,
    seoTitle: '',
    seoDescription: '',
    isAvailable: true,
    variants: [variantFixture()],
    images: [],
    ...overrides,
  };
}

export function pageFixture(
  items: unknown[],
  overrides: { page?: number; totalPages?: number; totalCount?: number } = {},
) {
  return {
    items,
    page: 1,
    pageSize: 12,
    totalCount: items.length,
    totalPages: items.length === 0 ? 0 : 1,
    ...overrides,
  };
}

export function cartLineFixture(overrides: Record<string, unknown> = {}) {
  return {
    variantId: 'v1',
    productName: 'Dragon',
    productSlug: 'dragon',
    variantName: 'Small',
    sku: 'DRG-S',
    image: null,
    unitPrice: 1250,
    quantity: 1,
    linePrice: 1250,
    state: 'Available',
    fulfilmentType: 'InStock',
    leadTimeDays: null,
    availableQuantity: 20,
    ...overrides,
  };
}

/** The cart as the server counts it: the sums come from the lines given. */
export function cartFixture(lines: Record<string, unknown>[] = []) {
  return {
    lines,
    itemCount: lines.reduce((sum, line) => sum + Number(line['quantity']), 0),
    total: lines.reduce((sum, line) => sum + Number(line['linePrice']), 0),
  };
}

/** What delivery costs for a cart worth 4500: one method, 600. The sums are the server's, so a test says them. */
export function shippingFixture(overrides: Record<string, unknown> = {}) {
  return { itemsTotal: 4500, methods: [{ code: 'address', amount: 600 }], ...overrides };
}
