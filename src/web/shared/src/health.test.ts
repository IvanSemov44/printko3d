import { afterEach, describe, expect, it, vi } from 'vitest';
import { checkApiHealth } from './health';

describe('checkApiHealth', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('is true when /health answers 200', async () => {
    const fetchMock = vi.fn(() => Promise.resolve(new Response(null, { status: 200 })));
    vi.stubGlobal('fetch', fetchMock);

    expect(await checkApiHealth('http://api.test')).toBe(true);
    expect(fetchMock).toHaveBeenCalledWith('http://api.test/health', { signal: undefined });
  });

  it('is false when /health answers 503', async () => {
    vi.stubGlobal('fetch', () => Promise.resolve(new Response(null, { status: 503 })));

    expect(await checkApiHealth('http://api.test')).toBe(false);
  });

  it('is false when the network request fails', async () => {
    vi.stubGlobal('fetch', () => Promise.reject(new TypeError('network down')));

    expect(await checkApiHealth('http://api.test')).toBe(false);
  });
});
