import { formatMoney, ORDER_STATUSES, type OrderStatus } from '@storefront/shared';
import { Link, useSearchParams } from 'react-router';
import { t } from '../../i18n';
import { useStoreMoney } from '../../components/useStoreMoney';
import { ORDER_STATUS_KEYS, orderDateText, orderNumberText } from './orderLabels';
import { useAdminOrders } from './useOrders';

const PAGE_SIZE = 20;
const MAX_PAGE = 10_000;
const SEARCH_MAX_LENGTH = 100;

interface Filters {
  status: OrderStatus | undefined;
  search: string;
  page: number;
}

/** The filters live in the address, so a reload, the back button and a copied link keep them. Bad values are ignored. */
function readFilters(params: URLSearchParams): Filters {
  const page = Number(params.get('page'));
  return {
    status: ORDER_STATUSES.find((item) => item === params.get('status')),
    search: (params.get('search') ?? '').slice(0, SEARCH_MAX_LENGTH),
    page: Number.isInteger(page) && page >= 1 && page <= MAX_PAGE ? page : 1,
  };
}

function writeFilters(filters: Filters): URLSearchParams {
  const params = new URLSearchParams();
  if (filters.status) {
    params.set('status', filters.status);
  }
  if (filters.search !== '') {
    params.set('search', filters.search);
  }
  if (filters.page > 1) {
    params.set('page', String(filters.page));
  }
  return params;
}

export function OrderListPage() {
  const [params, setParams] = useSearchParams();
  const filters = readFilters(params);
  const orders = useAdminOrders({
    ...(filters.status ? { status: [filters.status] } : {}),
    ...(filters.search !== '' ? { search: filters.search } : {}),
    page: filters.page,
    pageSize: PAGE_SIZE,
  });
  const money = useStoreMoney();

  /** Any change of a filter goes back to the first page. */
  function change(changes: Partial<Filters>) {
    setParams(writeFilters({ ...filters, page: 1, ...changes }));
  }

  const hasFilters = filters.status !== undefined || filters.search !== '';

  if (orders.isPending || money.isPending) {
    return <p role="status">{t('orders.loading')}</p>;
  }
  if (orders.isError || money.isError) {
    return (
      <>
        <p role="alert" className="form-error">
          {t('orders.error.loadFailed')}
        </p>
        <button
          type="button"
          onClick={() => {
            void orders.refetch();
            void money.refetch();
          }}
        >
          {t('auth.retry')}
        </button>
      </>
    );
  }

  const page = orders.data;
  const { locale } = money.data;

  return (
    <>
      <div className="page-header">
        <h2>{t('orders.title')}</h2>
      </div>

      <div className="filters" role="group" aria-label={t('orders.filters.label')}>
        <div className="field">
          <label htmlFor="filter-status">{t('orders.filters.status')}</label>
          <select
            id="filter-status"
            value={filters.status ?? ''}
            onChange={(event) => {
              change({ status: ORDER_STATUSES.find((item) => item === event.target.value) });
            }}
          >
            <option value="">{t('orders.filters.allStatuses')}</option>
            {ORDER_STATUSES.map((status) => (
              <option key={status} value={status}>
                {t(ORDER_STATUS_KEYS[status])}
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
            <label htmlFor="filter-search">{t('orders.filters.search')}</label>
            <input
              id="filter-search"
              name="search"
              type="search"
              autoComplete="off"
              maxLength={SEARCH_MAX_LENGTH}
              defaultValue={filters.search}
            />
          </div>
          <button type="submit">{t('orders.filters.searchButton')}</button>
        </form>

        {hasFilters ? (
          <button
            type="button"
            className="secondary"
            onClick={() => {
              setParams(new URLSearchParams());
            }}
          >
            {t('orders.filters.clear')}
          </button>
        ) : null}
      </div>

      {page.items.length === 0 ? (
        <p>{hasFilters ? t('orders.empty') : t('orders.emptyAll')}</p>
      ) : (
        <div className="table-wrap">
          <table className="product-table">
            <caption className="visually-hidden">{t('orders.table.caption')}</caption>
            <thead>
              <tr>
                <th scope="col">{t('orders.table.number')}</th>
                <th scope="col">{t('orders.table.date')}</th>
                <th scope="col">{t('orders.table.customer')}</th>
                <th scope="col">{t('orders.table.status')}</th>
                <th scope="col">{t('orders.table.items')}</th>
                <th scope="col">{t('orders.table.total')}</th>
              </tr>
            </thead>
            <tbody>
              {page.items.map((order) => (
                <tr key={order.number}>
                  <th scope="row">
                    <Link to={`/orders/${String(order.number)}`}>
                      {orderNumberText(order.number)}
                    </Link>
                  </th>
                  <td>{orderDateText(order.createdAt, locale)}</td>
                  <td>
                    <strong>{order.customerName}</strong>
                    <br />
                    <span className="field-hint">{order.email}</span>
                  </td>
                  <td>
                    <span className={`badge order-${order.status}`}>
                      {t(ORDER_STATUS_KEYS[order.status])}
                    </span>
                  </td>
                  <td>{order.itemCount}</td>
                  <td>{formatMoney(order.total, order.currency, locale)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {page.totalPages > 1 ? (
        <nav className="pagination" aria-label={t('orders.pagination.label')}>
          <button
            type="button"
            className="secondary"
            disabled={filters.page <= 1}
            onClick={() => {
              change({ page: filters.page - 1 });
            }}
          >
            {t('orders.pagination.previous')}
          </button>
          <span aria-live="polite">
            {t('orders.pagination.status', { page: page.page, pages: page.totalPages })}
          </span>
          <button
            type="button"
            className="secondary"
            disabled={filters.page >= page.totalPages}
            onClick={() => {
              change({ page: filters.page + 1 });
            }}
          >
            {t('orders.pagination.next')}
          </button>
        </nav>
      ) : null}
    </>
  );
}
