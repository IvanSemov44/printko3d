import type { Category } from '@storefront/shared';
import { useEffect, useRef } from 'react';
import { Link, Navigate, useSearchParams } from 'react-router';
import { LoadingStatus } from '../layout/LoadingStatus';
import { PageMeta } from '../seo/PageMeta';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';
import { CatalogError } from './CatalogError';
import { Pagination } from './Pagination';
import { ProductCard } from './ProductCard';
import { parsePageParam } from './pageParams';
import { useProductsQuery } from './useProductsQuery';

/** Heading, product list and paging for one known category. The page number comes from `?page=`. */
export function CategoryProducts({ category }: { category: Category }) {
  const t = useT();
  const { storeName } = useStoreSettings();
  const [searchParams] = useSearchParams();
  const page = parsePageParam(searchParams.get('page'));
  const { data, isError, isPlaceholderData, refetch } = useProductsQuery(category.slug, page);

  const headingRef = useRef<HTMLHeadingElement>(null);
  const shownPage = useRef(page);
  useEffect(() => {
    if (shownPage.current !== page) {
      shownPage.current = page;
      headingRef.current?.focus();
      headingRef.current?.scrollIntoView();
    }
  }, [page]);

  let content;
  if (isError) {
    content = <CatalogError onRetry={() => void refetch()} />;
  } else if (!data) {
    content = <LoadingStatus />;
  } else if (!isPlaceholderData && data.totalPages > 0 && page > data.totalPages) {
    const last = data.totalPages;
    content = <Navigate replace to={{ search: last === 1 ? '' : `?page=${String(last)}` }} />;
  } else if (data.totalPages === 0) {
    content = (
      <div className="empty-state">
        <p>{t('catalog.empty')}</p>
        <Link to="/" className="button">
          {t('catalog.emptyAction')}
        </Link>
      </div>
    );
  } else {
    content = (
      <>
        <ul className="product-list" aria-busy={isPlaceholderData}>
          {data.items.map((product) => (
            <ProductCard key={product.id} product={product} />
          ))}
        </ul>
        <Pagination currentPage={page} totalPages={data.totalPages} />
      </>
    );
  }

  return (
    <>
      <PageMeta
        pageTitle={page > 1 ? `${category.name} – ${t('seo.pageNumber', { page })}` : category.name}
        description={
          category.description !== null && category.description.trim() !== ''
            ? category.description
            : t('seo.categoryDescription', { category: category.name, storeName })
        }
        page={page}
        noindex={isError}
      />
      <h1 ref={headingRef} tabIndex={-1}>
        {category.name}
      </h1>
      {content}
    </>
  );
}
