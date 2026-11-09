import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [react()],
  server: {
    // In production nginx forwards /api/ to the API, so the browser talks to one address (decision 0014).
    // The dev server does the same, so the session cookie works in `npm run dev` too.
    proxy: { '/api': process.env.API_PROXY_TARGET ?? 'http://localhost:8080' },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test-setup.ts'],
  },
});
