import type { AdminCategory } from '@storefront/shared';
import { describe, expect, it } from 'vitest';
import { buildCategoryTree, siblingsOf } from './categoryTree';

function category(id: string, parentId: string | null = null): AdminCategory {
  return {
    id,
    parentId,
    name: id,
    slug: id,
    description: null,
    sortOrder: 0,
    isActive: true,
    productCount: 0,
    version: 1,
  };
}

describe('buildCategoryTree', () => {
  it('puts subcategories under their parent and keeps the order of the list', () => {
    const list = [
      category('b'),
      category('a'),
      category('b1', 'b'),
      category('a1', 'a'),
      category('b2', 'b'),
    ];

    const tree = buildCategoryTree(list);

    expect(tree.map((node) => node.category.id)).toEqual(['b', 'a']);
    expect(tree[0]?.children.map((child) => child.id)).toEqual(['b1', 'b2']);
    expect(tree[1]?.children.map((child) => child.id)).toEqual(['a1']);
  });

  it('shows a category with a missing parent at the top level instead of dropping it', () => {
    const tree = buildCategoryTree([category('a'), category('lost', 'gone')]);

    expect(tree.map((node) => node.category.id)).toEqual(['a', 'lost']);
  });

  it('gives an empty tree for an empty list', () => {
    expect(buildCategoryTree([])).toEqual([]);
  });
});

describe('siblingsOf', () => {
  const list = [category('a'), category('b'), category('a1', 'a'), category('a2', 'a')];

  it('finds the other top-level categories', () => {
    expect(siblingsOf(list, category('a')).map((c) => c.id)).toEqual(['a', 'b']);
  });

  it('finds the subcategories of the same parent only', () => {
    expect(siblingsOf(list, category('a1', 'a')).map((c) => c.id)).toEqual(['a1', 'a2']);
  });
});
