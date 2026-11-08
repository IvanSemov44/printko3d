import { ApiError } from '@storefront/shared';
import { useParams } from 'react-router';
import { CatalogError } from '../catalog/CatalogError';
import { NotFoundPage } from '../home/NotFoundPage';
import { LoadingStatus } from '../layout/LoadingStatus';
import { useT } from '../store-settings/useStoreSettings';
import { ProductDetailsView } from './ProductDetailsView';
import { useProductQuery } from './useProductQuery';

/** `/products/:slug`. A 404 from the API is the same "not found" page as an unknown address. */
export function ProductPage() {
  const t = useT();
  const { slug = '' } = useParams();
  const { data: product, error, isError, refetch } = useProductQuery(slug);

  if (isError) {
    if (error instanceof ApiError && error.status === 404) {
      return <NotFoundPage />;
    }
    return <CatalogError message={t('product.error')} onRetry={() => void refetch()} />;
  }
  if (!product) {
    return <LoadingStatus />;
  }
  return <ProductDetailsView product={product} />;
}
