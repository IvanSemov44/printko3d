import { ApiError, getMyOrder } from '@storefront/shared';
import { useQuery } from '@tanstack/react-query';
import { Link, Navigate, useParams } from 'react-router';
import { LoadingStatus } from '../../components/LoadingStatus';
import { parseOrderNumber } from '../orders/myOrderFailure';
import { OrderView } from '../orders/OrderView';
import { PageMeta } from '../seo/PageMeta';
import { useT } from '../store-settings/useStoreSettings';

/** `/account/orders/:number`. One order of the signed-in buyer (decision 0039). Another buyer's number and a missing one look the same. */
export function AccountOrderPage() {
  const t = useT();
  const params = useParams();
  const number = parseOrderNumber(params.number ?? '');
  const { data, error, isError, isPending } = useQuery({
    queryKey: ['customer', 'order', number],
    queryFn: ({ signal }) => getMyOrder(number ?? 0, signal),
    enabled: number !== null,
    retry: false,
  });

  if (number === null || (isError && error instanceof ApiError && error.status === 404)) {
    return (
      <>
        <PageMeta pageTitle={t('account.orders.title')} noindex />
        <h1>{t('account.orders.title')}</h1>
        <p role="alert">{t('account.orders.notFound')}</p>
        <Link className="button" to="/account/orders">
          {t('account.orders.title')}
        </Link>
      </>
    );
  }
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
      <OrderView order={data} />
      <p>
        <Link to="/account/orders">{t('account.orders.back')}</Link>
      </p>
    </>
  );
}
