import { cartFixture, cartLineFixture } from './renderApp';

/** A cart that behaves like the server: a change gives back the whole cart, and the sums are the server's. */
export function fakeServer(
  options: { initial?: Record<string, unknown>[]; failWith?: Response } = {},
) {
  let lines = options.initial ?? [];
  const requests: { method: string; url: string; headers: Headers; body: unknown }[] = [];
  const cart = (url: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET';
    const body = typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined;
    requests.push({ method, url, headers: new Headers(init?.headers), body });
    if (options.failWith && method !== 'GET') {
      return options.failWith.clone();
    }
    const variantId = url.split('/items/')[1];
    if (method === 'POST') {
      const asked = body as { variantId: string; quantity: number };
      lines = [
        ...lines.filter((line) => line['variantId'] !== asked.variantId),
        cartLineFixture({
          variantId: asked.variantId,
          quantity: asked.quantity,
          linePrice: 1250 * asked.quantity,
        }),
      ];
    } else if (method === 'PUT' && variantId) {
      const asked = body as { quantity: number };
      lines = lines.map((line) =>
        line['variantId'] === variantId
          ? {
              ...line,
              quantity: asked.quantity,
              linePrice: Number(line['unitPrice']) * asked.quantity,
            }
          : line,
      );
    } else if (method === 'DELETE') {
      // With an address of a line it removes the line; without one it clears the cart.
      lines = variantId ? lines.filter((line) => line['variantId'] !== variantId) : [];
    }
    return Response.json(cartFixture(lines));
  };
  return { cart, requests };
}
