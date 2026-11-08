import { useParams } from 'react-router';
import { NotFoundPage } from '../home/NotFoundPage';
import { LoadingStatus } from '../layout/LoadingStatus';
import { CatalogError } from './CatalogError';
import { CategoryProducts } from './CategoryProducts';
import { useCategoriesQuery } from './useCategoriesQuery';

/** `/categories/:slug`. The name comes from the categories list; a slug that is not in it is a missing page. */
export function CategoryPage() {
  const { slug } = useParams();
  const { data: categories, isError, refetch } = useCategoriesQuery();

  if (isError) {
    return <CatalogError onRetry={() => void refetch()} />;
  }
  if (!categories) {
    return <LoadingStatus />;
  }
  const category = categories.find((candidate) => candidate.slug === slug);
  if (!category) {
    return <NotFoundPage />;
  }
  // The key resets the focus and page tracking when the user moves to another category.
  return <CategoryProducts key={category.id} category={category} />;
}
