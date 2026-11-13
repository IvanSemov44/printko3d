/** An order as the API answers a placed order (decision 0032). */
export function orderFixture(overrides: Record<string, unknown> = {}) {
  return {
    number: 12345678,
    status: 'New',
    currency: 'EUR',
    total: 2500,
    customerName: 'Test Buyer',
    email: 'buyer@example.test',
    phone: '+359 88 000 0000',
    shippingStreet: '1 Test Street',
    shippingCity: 'Sofia',
    shippingPostalCode: '1000',
    shippingCountry: 'BG',
    createdAt: '2026-11-13T10:00:00+00:00',
    items: [
      {
        productName: 'Dragon',
        variantName: 'Small',
        sku: 'DRG-S',
        unitPrice: 1250,
        quantity: 2,
        lineTotal: 2500,
        fulfilmentType: 'InStock',
        leadTimeDays: null,
      },
    ],
    ...overrides,
  };
}
