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
    // The cart cookie belongs to the shop's own address (decision 0027), so `npm run dev` forwards the cart to the API
    // like nginx does in the image. Only the cart, the orders, the delivery prices and the payments; the address of the API is `API_DEV_URL` or the port of docker compose.
    proxy: {
      '^/api/v1/(cart|orders|shipping|payments)': {
        target: process.env['API_DEV_URL'] ?? 'http://localhost:8080',
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test-setup.ts'],
    // designTokens.test.ts reads index.css as text.
    css: { include: [/index\.css/] },
  },
});
