import type { ReactNode } from 'react';

/**
 * The table of every list in the panel (docs/design.md section 11). The caption is read by screen readers only.
 * A row is opened as a whole by giving one link in it the class `row-link`; wide tables scroll inside their panel.
 */
export function DataTable({ caption, children }: { caption: string; children: ReactNode }) {
  return (
    <div className="table-wrap">
      <table className="data-table">
        <caption className="visually-hidden">{caption}</caption>
        {children}
      </table>
    </div>
  );
}
