import { ApiError } from '@storefront/shared';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CART_KEY } from './cartKeys';
import {
  useAddToCart,
  useClearCart,
  useIsLineBusy,
  useRemoveFromCart,
  useSetCartQuantity,
} from './useCartMutations';
import { useCartQuery } from './useCartQuery';

const VARIANT = 'v1';

function cartBody(quantity: number) {
  return {
    lines:
      quantity === 0
        ? []
        : [
            {
              variantId: VARIANT,
              productName: 'Dragon',
              productSlug: 'dragon',
              variantName: 'Small',
              sku: 'DRG-S',
              image: null,
              unitPrice: 1250,
              quantity,
              linePrice: 1250 * quantity,
              state: 'Available',
            },
          ],
    itemCount: quantity,
    total: 1250 * quantity,
  };
}

function setup(respond: (url: string, init?: RequestInit) => Promise<Response>) {
  const fetchMock = vi.fn(respond);
  vi.stubGlobal('fetch', fetchMock);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  return { fetchMock, queryClient, wrapper };
}

function answer(body: unknown, status = 200) {
  return Promise.resolve(Response.json(body, { status }));
}

describe('cart hooks', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('reads the cart', async () => {
    const { wrapper } = setup(() => answer(cartBody(2)));

    const { result } = renderHook(() => useCartQuery(), { wrapper });

    await waitFor(() => {
      expect(result.current.data?.itemCount).toBe(2);
    });
  });

  it('puts the cart from the answer into the cache without asking again', async () => {
    const { fetchMock, queryClient, wrapper } = setup(() => answer(cartBody(1)));
    const { result } = renderHook(() => useAddToCart(VARIANT), { wrapper });

    await act(() => result.current.mutateAsync(1));

    expect(queryClient.getQueryData(CART_KEY)).toMatchObject({ itemCount: 1, total: 1250 });
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('replaces the cache with the answer of the server after a new quantity', async () => {
    const { queryClient, wrapper } = setup(() => answer(cartBody(3)));
    const { result } = renderHook(() => useSetCartQuantity(VARIANT), { wrapper });

    await act(() => result.current.mutateAsync(3));

    expect(queryClient.getQueryData(CART_KEY)).toMatchObject({ itemCount: 3, total: 3750 });
  });

  it('replaces the cache with the answer of the server after removing a line', async () => {
    const { queryClient, wrapper } = setup(() => answer(cartBody(0)));
    const { result } = renderHook(() => useRemoveFromCart(VARIANT), { wrapper });

    await act(() => result.current.mutateAsync());

    expect(queryClient.getQueryData(CART_KEY)).toMatchObject({ itemCount: 0, lines: [] });
  });

  it('replaces the cache with the answer of the server after clearing the cart', async () => {
    const { queryClient, wrapper } = setup(() => answer(cartBody(0)));
    const { result } = renderHook(() => useClearCart(), { wrapper });

    await act(() => result.current.mutateAsync());

    expect(queryClient.getQueryData(CART_KEY)).toMatchObject({ itemCount: 0, lines: [] });
  });

  it('keeps the error of the server and reads the cart again after a failed change', async () => {
    let reads = 0;
    const { queryClient, wrapper } = setup((_url, init) => {
      if (init?.method === 'PUT') {
        return answer({ code: 'cart.item_not_found' }, 404);
      }
      reads += 1;
      return answer(cartBody(0));
    });
    const { result: query } = renderHook(() => useCartQuery(), { wrapper });
    await waitFor(() => {
      expect(query.current.isSuccess).toBe(true);
    });
    const { result } = renderHook(() => useSetCartQuantity(VARIANT), { wrapper });

    let error: unknown;
    await act(async () => {
      try {
        await result.current.mutateAsync(2);
      } catch (caught) {
        error = caught;
      }
    });

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).code).toBe('cart.item_not_found');
    await waitFor(() => {
      expect(reads).toBe(2);
    });
    expect(queryClient.getQueryData(CART_KEY)).toMatchObject({ itemCount: 0 });
  });

  it('tells that a line is busy while its change is on its way, and not another line', async () => {
    let finish: (response: Response) => void = () => undefined;
    const { wrapper } = setup(
      () =>
        new Promise<Response>((resolve) => {
          finish = resolve;
        }),
    );
    const { result } = renderHook(
      () => ({
        add: useAddToCart(VARIANT),
        busy: useIsLineBusy(VARIANT),
        other: useIsLineBusy('v2'),
      }),
      { wrapper },
    );
    expect(result.current.busy).toBe(false);

    let pending: Promise<unknown> = Promise.resolve();
    act(() => {
      pending = result.current.add.mutateAsync(1);
    });
    await waitFor(() => {
      expect(result.current.busy).toBe(true);
    });
    expect(result.current.other).toBe(false);

    await act(async () => {
      finish(Response.json(cartBody(1)));
      await pending;
    });
    await waitFor(() => {
      expect(result.current.busy).toBe(false);
    });
  });

  it('sends changes to the cart one after the other, so an older answer cannot come last', async () => {
    const answers: ((response: Response) => void)[] = [];
    const { fetchMock, queryClient, wrapper } = setup(
      () =>
        new Promise<Response>((resolve) => {
          answers.push(resolve);
        }),
    );
    const { result } = renderHook(
      () => ({ first: useSetCartQuantity('v1'), second: useRemoveFromCart('v2') }),
      { wrapper },
    );

    let both: Promise<unknown> = Promise.resolve();
    act(() => {
      both = Promise.all([
        result.current.first.mutateAsync(2),
        result.current.second.mutateAsync(),
      ]);
    });
    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledTimes(1);
    });
    // The second change has not been sent while the first is on its way.
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await act(async () => {
      answers[0]?.(Response.json(cartBody(2)));
      await waitFor(() => {
        expect(fetchMock).toHaveBeenCalledTimes(2);
      });
      answers[1]?.(Response.json(cartBody(0)));
      await both;
    });

    expect(queryClient.getQueryData(CART_KEY)).toMatchObject({ itemCount: 0, lines: [] });
  });
});
