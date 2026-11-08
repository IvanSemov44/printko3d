export const PAGE_SIZE = 12;

/** Reads `?page=`: anything that is not a whole number of at least 1 means the first page. */
export function parsePageParam(raw: string | null): number {
  if (raw === null || !/^\d+$/.test(raw)) {
    return 1;
  }
  const page = Number(raw);
  return Number.isSafeInteger(page) && page >= 1 ? page : 1;
}

export type PageItem = number | 'gap';

/** Page numbers to show: the first, the last and the neighbours of the current page, with gaps between. */
export function getPageItems(current: number, total: number): PageItem[] {
  const wanted = new Set([1, total, current - 1, current, current + 1]);
  const pages = [...wanted].filter((page) => page >= 1 && page <= total).sort((a, b) => a - b);

  const items: PageItem[] = [];
  let previous = 0;
  for (const page of pages) {
    if (previous !== 0 && page - previous === 2) {
      items.push(previous + 1);
    } else if (previous !== 0 && page - previous > 2) {
      items.push('gap');
    }
    items.push(page);
    previous = page;
  }
  return items;
}
