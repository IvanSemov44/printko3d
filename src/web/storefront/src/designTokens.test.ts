import { contrastRatio } from '@storefront/shared';
import { describe, expect, it } from 'vitest';
import css from './index.css?raw';

const sources = import.meta.glob<string>(
  ['./**/*.css', './**/*.tsx', './**/*.ts', '!./**/*.test.*', '!./testing/**', '!./test-setup.ts'],
  {
    query: '?raw',
    import: 'default',
    eager: true,
  },
);

function tokens(): Record<string, string> {
  const root = /:root\s*\{([^}]*)\}/.exec(css)?.[1] ?? '';
  const result: Record<string, string> = {};
  for (const match of root.matchAll(/--color-([a-z-]+):\s*(#[0-9a-f]{6});/g)) {
    result[match[1] as string] = match[2] as string;
  }
  return result;
}

const t = tokens();

describe('design tokens', () => {
  it('define every colour of docs/design.md', () => {
    for (const name of [
      'bg',
      'surface',
      'surface-alt',
      'ink',
      'ink-muted',
      'border',
      'primary',
      'primary-hover',
      'on-primary',
      'secondary',
      'on-secondary',
      'success',
      'warning',
      'error',
      'info',
    ]) {
      expect(t, name).toHaveProperty(name);
    }
  });

  // Text pairs need 4.5:1 (WCAG 1.4.3), components 3:1 (1.4.11).
  it.each([
    ['ink', 'bg', 4.5],
    ['ink', 'surface', 4.5],
    ['ink-muted', 'bg', 4.5],
    ['ink-muted', 'surface', 4.5],
    ['ink-muted', 'surface-alt', 4.5],
    ['secondary', 'bg', 4.5],
    ['secondary', 'surface', 4.5],
    ['on-primary', 'primary', 4.5],
    ['on-primary', 'primary-hover', 4.5],
    ['on-secondary', 'secondary', 4.5],
    ['ink', 'accent-yellow', 4.5],
    ['success', 'bg', 4.5],
    ['warning', 'bg', 4.5],
    ['error', 'bg', 4.5],
    ['info', 'bg', 4.5],
    ['success', 'surface', 4.5],
    ['warning', 'surface', 4.5],
    ['error', 'surface', 4.5],
    ['info', 'surface', 4.5],
    ['primary', 'surface', 3],
    ['ink', 'border', 3],
  ] as const)('%s on %s is at least %s:1', (foreground, background, minimum) => {
    expect(contrastRatio(t[foreground] as string, t[background] as string)).toBeGreaterThanOrEqual(
      minimum,
    );
  });

  it('are used without the old names and without literal colours outside the definition', () => {
    const problems: string[] = [];
    for (const [file, text] of Object.entries(sources)) {
      const code = file.endsWith('.css') ? text.replace(/:root\s*\{[^}]*\}/, '') : text;
      if (/--color-(text|muted)\b/.test(code)) {
        problems.push(`${file}: old token name`);
      }
      if (/#[0-9a-fA-F]{6}\b|#[0-9a-fA-F]{3}\b(?![\w-])/.test(code) && !file.endsWith('theme.ts')) {
        problems.push(`${file}: literal colour`);
      }
    }
    expect(problems).toEqual([]);
  });
});
