import { ApiError, formatMoney, getMyOrders } from '@storefront/shared';
import { useQuery } from '@tanstack/react-query';
import { Link, Navigate, useSearchParams } from 'react-router';
import { LoadingStatus } from '../../components/LoadingStatus';
import { Pagination } from '../catalog/Pagination';
import { parsePageParam } from '../catalog/pageParams';
import { statusText } from '../orders/orderStatus';
import { PageMeta } from '../seo/PageMeta';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';

/** How many orders a page of the history shows. The server allows at most 100. */
const ORDERS_PAGE_SIZE = 10;

/** `/account/orders`. The orders of the signed-in buyer, the newest first, one page at a time (decision 0039). */
export function AccountOrdersPage() {
  const t = useT();
  const { defaultLocale } = useStoreSettings();
  const [params] = useSearchParams();
  const page = parsePageParam(params.get('page'));
  const { data, error, isError, isPending } = useQuery({
    queryKey: ['customer', 'orders', page],
    queryFn: ({ signal }) => getMyOrders({ page, pageSize: ORDERS_PAGE_SIZE }, signal),
    retry: false,
  });

  if (isPending) {
    return <LoadingStatus />;
  }
  if (isError) {
    return error instanceof ApiError && error.status === 401 ? (
      <Navigate to="/account/login" replace />
    ) : (
      <p role="alert">{t('account.error.generic')}</p>
    );
  }

  return (
    <>
      <PageMeta pageTitle={t('account.orders.title')} noindex />
      <h1>{t('account.orders.title')}</h1>
      {data.items.length === 0 ? (
        <p>{t('account.orders.empty')}</p>
      ) : (
        <table className="orders-table">
          <caption className="visually-hidden">{t('account.orders.title')}</caption>
          <thead>
            <tr>
              <th scope="col">{t('account.orders.number')}</th>
              <th scope="col">{t('account.orders.date')}</th>
              <th scope="col">{t('account.orders.status')}</th>
              <th scope="col">{t('account.orders.total')}</th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((order) => (
              <tr key={order.number}>
                <th scope="row">
                  <Link
                    to={`/account/orders/${String(order.number)}`}
                  >{`PR-${String(order.number)}`}</Link>
                </th>
                <td>{new Date(order.createdAt).toLocaleDateString(defaultLocale)}</td>
                <td>{t(statusText(order.status))}</td>
                <td>{formatMoney(order.total, order.currency, defaultLocale)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <Pagination currentPage={data.page} totalPages={data.totalPages} />
      <p>
        <Link to="/account">{t('account.title')}</Link>
      </p>
    </>
  );
}
