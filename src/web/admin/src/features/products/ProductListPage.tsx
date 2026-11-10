import { formatMoney, type AdminProductListItem, type ProductStatus } from '@storefront/shared';
import { Link, useSearchParams } from 'react-router';
import { t } from '../../i18n';
import { useAdminCategories } from '../categories/useCategories';
import { FULFILMENT_KEYS, PRODUCT_STATUSES, STATUS_KEYS } from './productLabels';
import { parseWholeNumber } from './productValidation';
import { useAdminProducts, useStoreMoney } from './useProducts';

const PAGE_SIZE = 20;
const SEARCH_MAX_LENGTH = 100;

interface Filters {
  status: ProductStatus | undefined;
  categoryId: string;
  search: string;
  page: number;
}

/** The filters live in the address, so a reload, the back button and a copied link keep them. Bad values are ignored. */
function readFilters(params: URLSearchParams): Filters {
  const status = PRODUCT_STATUSES.find((item) => item === params.get('status'));
  const page = parseWholeNumber(params.get('page') ?? '');
  return {
    status,
    categoryId: params.get('categoryId') ?? '',
    search: (params.get('search') ?? '').slice(0, SEARCH_MAX_LENGTH),
    page: page === null || page < 1 ? 1 : page,
  };
}

function writeFilters(filters: Filters): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.status) {
    params.set('status', filters.status);
  }
  if (filters.categoryId !== '') {
    params.set('categoryId', filters.categoryId);
  }
  if (filters.search !== '') {
    params.set('search', filters.search);
  }
  if (filters.page > 1) {
    params.set('page', String(filters.page));
  }
  return params;
}

function ProductRow({
  product,
  priceText,
}: {
  product: AdminProductListItem;
  priceText: (minPrice: number) => string;
}) {
  return (
    <tr>
      <th scope="row">
        <strong>{product.name}</strong>
        <br />
        <span className="field-hint">{product.slug}</span>
      </th>
      <td>{product.categoryName}</td>
      <td>
        <span className={`badge status-${product.status}`}>{t(STATUS_KEYS[product.status])}</span>
      </td>
      <td>{t(FULFILMENT_KEYS[product.fulfilmentType])}</td>
      <td>{product.variantCount}</td>
      <td>
        {product.minPrice === null ? t('products.table.noPrice') : priceText(product.minPrice)}
      </td>
      <td>{product.totalStock === null ? t('products.table.noStock') : product.totalStock}</td>
      <td>
        <Link
          className="button secondary"
          to={`/products/${product.id}`}
          aria-label={t('products.edit', { name: product.name })}
        >
          {t('products.editShort')}
        </Link>
      </td>
    </tr>
  );
}

export function ProductListPage() {
  const [params, setParams] = useSearchParams();
  const filters = readFilters(params);
  const products = useAdminProducts({
    ...(filters.status ? { status: filters.status } : {}),
    ...(filters.categoryId !== '' ? { categoryId: filters.categoryId } : {}),
    ...(filters.search !== '' ? { search: filters.search } : {}),
    page: filters.page,
    pageSize: PAGE_SIZE,
  });
  const categories = useAdminCategories();
  const money = useStoreMoney();

  /** Any change of a filter goes back to the first page. */
  function change(changes: Partial<Filters>) {
    setParams(writeFilters({ ...filters, page: 1, ...changes }));
  }

  const hasFilters =
    filters.status !== undefined || filters.categoryId !== '' || filters.search !== '';

  if (products.isPending || money.isPending) {
    return <p role="status">{t('products.loading')}</p>;
  }
  if (products.isError || money.isError) {
    return (
      <>
        <p role="alert" className="form-error">
          {t('products.error.loadFailed')}
        </p>
        <button
          type="button"
          onClick={() => {
            void products.refetch();
            void money.refetch();
          }}
        >
          {t('auth.retry')}
        </button>
      </>
    );
  }

  const page = products.data;
  const priceText = (minPrice: number) =>
    formatMoney(minPrice, money.data.currency, money.data.locale);

  return (
    <>
      <div className="page-header">
        <h2>{t('products.title')}</h2>
        <Link className="button" to="/products/new">
          {t('products.new')}
        </Link>
      </div>

      <div className="filters" role="group" aria-label={t('products.filters.label')}>
        <div className="field">
          <label htmlFor="filter-status">{t('products.filters.status')}</label>
          <select
            id="filter-status"
            value={filters.status ?? ''}
            onChange={(event) => {
              change({ status: PRODUCT_STATUSES.find((item) => item === event.target.value) });
            }}
          >
            <option value="">{t('products.filters.allStatuses')}</option>
            {PRODUCT_STATUSES.map((status) => (
              <option key={status} value={status}>
                {t(STATUS_KEYS[status])}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label htmlFor="filter-category">{t('products.filters.category')}</label>
          <select
            id="filter-category"
            value={filters.categoryId}
            onChange={(event) => {
              change({ categoryId: event.target.value });
            }}
          >
            <option value="">{t('products.filters.allCategories')}</option>
            {(categories.data ?? []).map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </div>

        {/* The key starts the form again when the search in the address changes, for example after "Clear". */}
        <form
          key={filters.search}
          role="search"
          onSubmit={(event) => {
            event.preventDefault();
            const input = event.currentTarget.elements.namedItem('search');
            change({ search: input instanceof HTMLInputElement ? input.value.trim() : '' });
          }}
        >
          <div className="field">
            <label htmlFor="filter-search">{t('products.filters.search')}</label>
            <input
              id="filter-search"
              name="search"
              type="search"
              autoComplete="off"
              maxLength={SEARCH_MAX_LENGTH}
              defaultValue={filters.search}
            />
          </div>
          <button type="submit">{t('products.filters.searchButton')}</button>
        </form>

        {hasFilters ? (
          <button
            type="button"
            className="secondary"
            onClick={() => {
              setParams(new URLSearchParams());
            }}
          >
            {t('products.filters.clear')}
          </button>
        ) : null}
      </div>

      {page.items.length === 0 ? (
        <p>{hasFilters ? t('products.empty') : t('products.emptyAll')}</p>
      ) : (
        <div className="table-wrap">
          <table className="product-table">
            <caption className="visually-hidden">{t('products.table.caption')}</caption>
            <thead>
              <tr>
                <th scope="col">{t('products.table.name')}</th>
                <th scope="col">{t('products.table.category')}</th>
                <th scope="col">{t('products.table.status')}</th>
                <th scope="col">{t('products.table.fulfilment')}</th>
                <th scope="col">{t('products.table.variants')}</th>
                <th scope="col">{t('products.table.price')}</th>
                <th scope="col">{t('products.table.stock')}</th>
                <th scope="col">
                  <span className="visually-hidden">{t('products.table.actions')}</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {page.items.map((product) => (
                <ProductRow key={product.id} product={product} priceText={priceText} />
              ))}
            </tbody>
          </table>
        </div>
      )}

      {page.totalPages > 1 ? (
        <nav className="pagination" aria-label={t('products.pagination.label')}>
          <button
            type="button"
            className="secondary"
            disabled={filters.page <= 1}
            onClick={() => {
              change({ page: filters.page - 1 });
            }}
          >
            {t('products.pagination.previous')}
          </button>
          <span aria-live="polite">
            {t('products.pagination.status', { page: page.page, pages: page.totalPages })}
          </span>
          <button
            type="button"
            className="secondary"
            disabled={filters.page >= page.totalPages}
            onClick={() => {
              change({ page: filters.page + 1 });
            }}
          >
            {t('products.pagination.next')}
          </button>
        </nav>
      ) : null}
    </>
  );
}
