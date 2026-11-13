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
      fulfilmentType: 'InStock',
      leadTimeDays: null,
      availableQuantity: 4,
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
    expect(fetchMock.mock.calls[0]?.[1]?.credentials).toBe('same-origin');
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

  it('reads how a line is fulfilled and how many pieces can be taken', async () => {
    stubFetch({
      ...DTO,
      lines: [
        DTO.lines[0],
        {
          ...DTO.lines[0],
          fulfilmentType: 'MadeToOrder',
          leadTimeDays: '7',
          availableQuantity: null,
        },
      ],
    });

    const cart = await getCart();

    expect(cart.lines[0]).toMatchObject({
      fulfilmentType: 'InStock',
      leadTimeDays: null,
      availableQuantity: 4,
    });
    expect(cart.lines[1]).toMatchObject({
      fulfilmentType: 'MadeToOrder',
      leadTimeDays: 7,
      availableQuantity: null,
    });
  });

  it('refuses a line with an unknown way of fulfilment', async () => {
    stubFetch({ ...DTO, lines: [{ ...DTO.lines[0], fulfilmentType: 'Magic' }] });

    await expect(getCart()).rejects.toThrow(/lines\[0\]\.fulfilmentType/);
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

  it('reads the cart from the 201 answer of an add that made a new line', async () => {
    stubFetch(DTO, 201);

    const cart = await addCartItem(VARIANT_ID, 1);

    expect(cart.lines).toHaveLength(1);
    expect(cart.itemCount).toBe(2);
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

  describe('removing', () => {
    // The API answers a DELETE with 204 and no body (rule 6 of decision 0017); the new state is read with a GET.
    function stubDelete() {
      const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(
        (_url, init) =>
          Promise.resolve(
            init?.method === 'DELETE' ? new Response(null, { status: 204 }) : Response.json(DTO),
          ),
      );
      vi.stubGlobal('fetch', fetchMock);
      return fetchMock;
    }

    it('removes a line with the header and no body, then reads the cart', async () => {
      const fetchMock = stubDelete();

      const cart = await removeCartItem(VARIANT_ID);

      const [removeUrl, removeInit] = fetchMock.mock.calls[0] ?? [];
      expect(removeUrl).toBe(`/api/v1/cart/items/${VARIANT_ID}`);
      expect(removeInit?.method).toBe('DELETE');
      expect(headersOf(removeInit).get('X-Requested-With')).toBe('XMLHttpRequest');
      expect(removeInit?.body).toBeUndefined();
      const [readUrl, readInit] = fetchMock.mock.calls[1] ?? [];
      expect(readUrl).toBe('/api/v1/cart');
      expect(readInit?.method).toBeUndefined();
      expect(cart.lines).toHaveLength(1);
    });

    it('clears the cart with the header and no body, then reads the cart', async () => {
      const fetchMock = stubDelete();

      const cart = await clearCart();

      const [clearUrl, clearInit] = fetchMock.mock.calls[0] ?? [];
      expect(clearUrl).toBe('/api/v1/cart');
      expect(clearInit?.method).toBe('DELETE');
      expect(headersOf(clearInit).get('X-Requested-With')).toBe('XMLHttpRequest');
      expect(fetchMock.mock.calls[1]?.[0]).toBe('/api/v1/cart');
      expect(cart.itemCount).toBe(DTO.itemCount);
    });

    it('does not read the cart when the removal failed', async () => {
      const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>(() =>
        Promise.resolve(Response.json({ code: 'cart.item_not_found' }, { status: 404 })),
      );
      vi.stubGlobal('fetch', fetchMock);

      await expect(removeCartItem(VARIANT_ID)).rejects.toMatchObject({
        status: 404,
        code: 'cart.item_not_found',
      });
      expect(fetchMock).toHaveBeenCalledTimes(1);
    });
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
