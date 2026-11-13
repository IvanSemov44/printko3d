import { NavLink } from 'react-router';
import { useCategoriesQuery } from '../../components/useCategoriesQuery';
import { useT } from '../store-settings/useStoreSettings';

/** Top-level categories. If they fail to load the menu is left out; the page still works. */
export function CategoryNav() {
  const t = useT();
  const { data } = useCategoriesQuery();
  const topLevel = data?.filter((category) => category.parentId === null) ?? [];

  if (topLevel.length === 0) {
    return null;
  }
  return (
    <nav aria-label={t('layout.categoriesNav')}>
      <ul className="category-nav">
        {topLevel.map((category) => (
          <li key={category.id}>
            <NavLink to={`/categories/${category.slug}`}>{category.name}</NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
