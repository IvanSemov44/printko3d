import type { AdminCategory } from '@storefront/shared';

export interface CategoryNode {
  category: AdminCategory;
  children: AdminCategory[];
}

/**
 * Groups the flat list into top-level categories with their subcategories. The API already orders the list by
 * sortOrder, name and id, and that order is kept. A category whose parent is missing is shown at the top level
 * rather than dropped, so nothing the API returned can disappear from the screen.
 */
export function buildCategoryTree(categories: readonly AdminCategory[]): CategoryNode[] {
  const ids = new Set(categories.map((category) => category.id));
  const roots = categories.filter(
    (category) => category.parentId === null || !ids.has(category.parentId),
  );
  return roots.map((category) => ({
    category,
    children: categories.filter((child) => child.parentId === category.id),
  }));
}

/** The categories with the same parent (the top level counts as one parent), in the order the API gave them. */
export function siblingsOf(
  categories: readonly AdminCategory[],
  category: AdminCategory,
): AdminCategory[] {
  const ids = new Set(categories.map((item) => item.id));
  const isTopLevel = (item: AdminCategory) => item.parentId === null || !ids.has(item.parentId);
  return categories.filter((item) =>
    isTopLevel(category) ? isTopLevel(item) : item.parentId === category.parentId,
  );
}
