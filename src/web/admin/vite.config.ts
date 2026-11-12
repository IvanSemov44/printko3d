import babel from '@rolldown/plugin-babel';
import react, { reactCompilerPreset } from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [
    react(),
    // React Compiler memoizes components and hooks at build time (decision 0025).
    // Tests run without it: they check behaviour, and the build proves the compiler works (see the decision).
    ...(process.env['VITEST'] ? [] : [babel({ presets: [reactCompilerPreset()] })]),
  ],
  resolve: {
    alias: {
      '@storefront/shared': fileURLToPath(new URL('../shared/src/index.ts', import.meta.url)),
    },
  },
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
