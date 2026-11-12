import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from './client';
import { addCartItem, clearCart, getCart, removeCartItem, setCartItemQuantity } from './cart';

const VARIANT_ID = '7f1c2d3e-0000-4000-8000-000000000001';

const DTO = {
  lines: [
    {
      variantId: VARIANT_ID,
      productName: 'Вазa',
      productSlug: 'vaza',
      variantName: 'Небесно син',
      sku: 'VAZ-BLUE',
      image: { storageKey: 'products/vaza.webp', altText: 'Синя ваза' },
      unitPrice: '1250',
      quantity: 2,
      linePrice: 2500,
      state: 'Available',
    },
  ],
  itemCount: 2,
  total: '2500',
};

function stubFetch(body: unknown, status = 200) {
  const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(() =>
    Promise.resolve(Response.json(body, { status })),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function headersOf(init: RequestInit | undefined): Headers {
  return new Headers(init?.headers);
}

describe('cart API', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('reads the cart from the shop address, without the header that is for changes', async () => {
    const fetchMock = stubFetch(DTO);

    const cart = await getCart();

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/v1/cart');
    expect(headersOf(fetchMock.mock.calls[0]?.[1]).has('X-Requested-With')).toBe(false);
    expect(cart.itemCount).toBe(2);
    expect(cart.total).toBe(2500);
    expect(cart.lines[0]).toMatchObject({
      variantId: VARIANT_ID,
      unitPrice: 1250,
      linePrice: 2500,
      state: 'Available',
      image: { storageKey: 'products/vaza.webp', altText: 'Синя ваза' },
    });
  });

  it('reads a line without an image', async () => {
    stubFetch({ ...DTO, lines: [{ ...DTO.lines[0], image: null }] });

    const cart = await getCart();

    expect(cart.lines[0]?.image).toBeNull();
  });

  it('adds an item with the header that protects against cross-site requests', async () => {
    const fetchMock = stubFetch(DTO);

    await addCartItem(VARIANT_ID, 1);

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/api/v1/cart/items');
    expect(init?.method).toBe('POST');
    expect(headersOf(init).get('X-Requested-With')).toBe('XMLHttpRequest');
    expect(JSON.parse(init?.body as string)).toEqual({ variantId: VARIANT_ID, quantity: 1 });
  });

  it('changes the quantity of a line', async () => {
    const fetchMock = stubFetch(DTO);

    await setCartItemQuantity(VARIANT_ID, 5);

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe(`/api/v1/cart/items/${VARIANT_ID}`);
    expect(init?.method).toBe('PUT');
    expect(headersOf(init).get('X-Requested-With')).toBe('XMLHttpRequest');
    expect(JSON.parse(init?.body as string)).toEqual({ quantity: 5 });
  });

  it('removes a line and clears the cart, both with the header and without a body', async () => {
    const fetchMock = stubFetch(DTO);

    await removeCartItem(VARIANT_ID);
    await clearCart();

    const [removeUrl, removeInit] = fetchMock.mock.calls[0] ?? [];
    expect(removeUrl).toBe(`/api/v1/cart/items/${VARIANT_ID}`);
    expect(removeInit?.method).toBe('DELETE');
    expect(headersOf(removeInit).get('X-Requested-With')).toBe('XMLHttpRequest');
    expect(removeInit?.body).toBeUndefined();
    const [clearUrl, clearInit] = fetchMock.mock.calls[1] ?? [];
    expect(clearUrl).toBe('/api/v1/cart');
    expect(clearInit?.method).toBe('DELETE');
    expect(headersOf(clearInit).get('X-Requested-With')).toBe('XMLHttpRequest');
  });

  it('turns a business limit into an error with the code of the server', async () => {
    stubFetch({ code: 'cart.quantity_too_high' }, 422);

    const error: unknown = await addCartItem(VARIANT_ID, 1).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(422);
    expect((error as ApiError).code).toBe('cart.quantity_too_high');
  });

  it('refuses an answer with an unknown line state', async () => {
    stubFetch({ ...DTO, lines: [{ ...DTO.lines[0], state: 'Mystery' }] });

    await expect(getCart()).rejects.toThrow(/lines\[0\]\.state/);
  });

  it('passes the abort signal on', async () => {
    const fetchMock = stubFetch(DTO);
    const controller = new AbortController();

    await getCart(controller.signal);

    expect(fetchMock.mock.calls[0]?.[1]?.signal).toBe(controller.signal);
  });
});
