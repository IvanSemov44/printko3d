import { ApiError } from '@storefront/shared';
import { Link, useParams } from 'react-router';
import { StatusBadge } from '../../components/StatusBadge';
import { useStoreMoney } from '../../components/useStoreMoney';
import { t } from '../../i18n';
import { orderDateText, orderNumberText, orderStatusText, orderStatusTone } from './orderLabels';
import { OrderActions } from './OrderActions';
import { OrderLines } from './OrderLines';
import { useAdminOrder } from './useOrders';

export function OrderDetailPage() {
  const params = useParams();
  const number = Number(params.number);
  const valid = Number.isSafeInteger(number) && number > 0;
  const order = useAdminOrder(valid ? number : 0);
  const money = useStoreMoney();

  const back = <Link to="/orders">{t('orders.detail.back')}</Link>;

  if (!valid || (order.error instanceof ApiError && order.error.status === 404)) {
    return (
      <>
        <p role="alert">{t('orders.detail.notFound')}</p>
        {back}
      </>
    );
  }
  if (order.isPending || money.isPending) {
    return <p role="status">{t('orders.loading')}</p>;
  }
  if (order.isError || money.isError) {
    return (
      <>
        <p role="alert" className="form-error">
          {t('orders.error.loadFailed')}
        </p>
        <button
          type="button"
          onClick={() => {
            void order.refetch();
            void money.refetch();
          }}
        >
          {t('auth.retry')}
        </button>
        {back}
      </>
    );
  }

  const { locale } = money.data;
  const data = order.data;
  const placed = data.order;

  return (
    <>
      {back}
      <div className="page-header">
        <h2>{t('orders.detail.title', { number: orderNumberText(placed.number) })}</h2>
        <StatusBadge tone={orderStatusTone(placed.status)}>
          {orderStatusText(placed.status)}
        </StatusBadge>
      </div>

      <section className="section" aria-labelledby="order-actions">
        <h3 id="order-actions">{t('orders.detail.actions')}</h3>
        <OrderActions data={data} />
      </section>

      <section className="section" aria-labelledby="order-items">
        <h3 id="order-items">{t('orders.detail.items')}</h3>
        <OrderLines order={placed} locale={locale} />
      </section>

      <section className="section" aria-labelledby="order-contact">
        <h3 id="order-contact">{t('orders.detail.contact')}</h3>
        <dl className="detail-list">
          <dt>{t('orders.contact.name')}</dt>
          <dd>{placed.customerName}</dd>
          <dt>{t('orders.contact.email')}</dt>
          <dd>{placed.email}</dd>
          <dt>{t('orders.contact.phone')}</dt>
          <dd>{placed.phone}</dd>
          <dt>{t('orders.contact.address')}</dt>
          <dd>
            {placed.shippingStreet}, {placed.shippingPostalCode} {placed.shippingCity},{' '}
            {placed.shippingCountry}
          </dd>
          <dt>{t('orders.contact.created')}</dt>
          <dd>{orderDateText(placed.createdAt, locale)}</dd>
          {placed.paymentDueAt ? (
            <>
              <dt>{t('orders.contact.paymentDue')}</dt>
              <dd>{orderDateText(placed.paymentDueAt, locale)}</dd>
            </>
          ) : null}
        </dl>
      </section>
    </>
  );
}
