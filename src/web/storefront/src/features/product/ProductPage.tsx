import { ApiError } from '@storefront/shared';
import { useParams } from 'react-router';
import { CatalogError } from '../../components/CatalogError';
import { NotFoundPage } from '../../components/NotFoundPage';
import { PageMeta } from '../seo/PageMeta';
import { LoadingStatus } from '../../components/LoadingStatus';
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
    return (
      <>
        <PageMeta noindex />
        <CatalogError message={t('product.error')} onRetry={() => void refetch()} />
      </>
    );
  }
  if (!product) {
    return (
      <>
        <PageMeta noindex />
        <LoadingStatus />
      </>
    );
  }
  return <ProductDetailsView product={product} />;
}
