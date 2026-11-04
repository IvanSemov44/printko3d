import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';

function stubFetch(healthOk: boolean) {
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string) =>
      Promise.resolve(
        url === '/config.json'
          ? Response.json({ apiUrl: 'http://api.test' })
          : new Response(null, { status: healthOk ? 200 : 503 }),
      ),
    ),
  );
}

describe('App', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows that the API is available when /health succeeds', async () => {
    stubFetch(true);
    render(<App />);

    expect(await screen.findByText('API е достъпно')).toBeInTheDocument();
  });

  it('shows that the API is unavailable when /health fails', async () => {
    stubFetch(false);
    render(<App />);

    expect(await screen.findByText('API не е достъпно')).toBeInTheDocument();
  });
});
