import type { Category } from '@storefront/shared';
import { Link } from 'react-router';
import { ProductCard } from '../../components/ProductCard';
import { useT } from '../store-settings/useStoreSettings';
import { useCategoryPreviewQuery } from './useCategoryPreviewQuery';

/** A few products of one category with a link to all of them. Left out while loading, on error and when empty. */
export function CategoryPreview({ category }: { category: Category }) {
  const t = useT();
  const { data } = useCategoryPreviewQuery(category.slug);

  if (!data || data.items.length === 0) {
    return null;
  }
  return (
    <section className="home-section" aria-labelledby={`home-${category.slug}`}>
      <div className="home-section-header">
        <h2 id={`home-${category.slug}`}>{category.name}</h2>
        <Link
          to={`/categories/${category.slug}`}
          className="button button-text"
          aria-label={t('home.viewAllIn', { category: category.name })}
        >
          {t('home.viewAll')}
        </Link>
      </div>
      <ul className="product-list">
        {data.items.map((product) => (
          <ProductCard key={product.id} product={product} />
        ))}
      </ul>
    </section>
  );
}
