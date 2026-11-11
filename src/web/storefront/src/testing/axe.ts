import axe from 'axe-core';

/** WCAG 2.0 and 2.1 level A and AA, plus axe's best practices (landmarks, one h1, heading order). */
const TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'best-practice'];

/**
 * Runs axe over the whole document and returns a readable line for each violated rule; an empty list means none.
 * jsdom draws nothing, so axe cannot measure colours: `color-contrast` is off here. The colour of the theme is checked
 * by the contrast tests in `@storefront/shared` (`color.test.ts`) and by hand on a real page (see the pull request).
 */
export async function findAccessibilityViolations(): Promise<string[]> {
  // index.html has this; jsdom starts without it.
  document.documentElement.lang = 'bg';
  const results = await axe.run(document, {
    runOnly: { type: 'tag', values: TAGS },
    rules: { 'color-contrast': { enabled: false } },
  });
  return results.violations.map(
    (violation) =>
      `${violation.id}: ${violation.help} -> ${violation.nodes.map((node) => node.target.join(' ')).join(' | ')}`,
  );
}
