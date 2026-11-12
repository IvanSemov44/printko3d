import babel from '@rolldown/plugin-babel';
import react, { reactCompilerPreset } from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [
    react(),
    // React Compiler memoizes components and hooks at build time (decision 0025).
    // Tests run without it: they check behaviour, and the build proves the compiler works (see the decision).
    ...(process.env['VITEST'] ? [] : [babel({ presets: [reactCompilerPreset()] })]),
  ],
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test-setup.ts'],
  },
});
