import type { ReactNode } from 'react';

/** The colour of a pill. The state is always a word as well, never only a colour (docs/design.md section 11). */
export type BadgeTone = 'info' | 'production' | 'success' | 'teal' | 'error' | 'muted';

export function StatusBadge({ tone, children }: { tone: BadgeTone; children: ReactNode }) {
  return <span className={`pill pill-${tone}`}>{children}</span>;
}
