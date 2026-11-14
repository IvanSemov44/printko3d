import { readOrder, type Order } from '@storefront/shared';
import { Link, useLocation, useParams } from 'react-router';
import { PageMeta } from '../seo/PageMeta';
import { PaymentSection } from './PaymentSection';
import { OrderDetails } from './OrderDetails';
import { useT } from '../store-settings/useStoreSettings';

/**
 * The order that the checkout put in the state of the route, if it is the one of this address. There is no address
 * to read an order again (debt D65), so a fresh visit, a wrong shape or another number gives nothing.
 */
function orderFromState(state: unknown, number: string | undefined): Order | null {
  if (typeof state !== 'object' || state === null || !('order' in state)) {
    return null;
  }
  try {
    const order = readOrder(state.order);
    return String(order.number) === number ? order : null;
  } catch {
    return null;
  }
}

/** The payment the buyer is coming back from, if the state of the route names one. */
function paymentIdFromState(state: unknown): string | null {
  return typeof state === 'object' &&
    state !== null &&
    'paymentId' in state &&
    typeof state.paymentId === 'string'
    ? state.paymentId
    : null;
}

/** `/order/:number`. What was ordered and how to pay it, or how the payment went. Kept out of search results. */
export function OrderConfirmationPage() {
  const t = useT();
  const { number } = useParams();
  const location = useLocation();
  const order = orderFromState(location.state, number);

  if (order === null) {
    return (
      <>
        <PageMeta pageTitle={t('checkout.title')} noindex />
        <h1>{t('checkout.title')}</h1>
        <div className="empty-state">
          <p>{t('order.gone')}</p>
          <Link className="button" to="/">
            {t('order.toStart')}
          </Link>
        </div>
      </>
    );
  }

  return (
    <>
      <PageMeta pageTitle={t('order.title')} noindex />
      <h1>{t('order.title')}</h1>
      <p className="order-number">
        {t('order.numberLabel')} <strong>{`PR-${String(order.number)}`}</strong>
      </p>
      <PaymentSection order={order} paymentId={paymentIdFromState(location.state)} />
      <p>
        <Link to="/my-order">{t('order.track')}</Link>
      </p>
      <OrderDetails order={order} />
    </>
  );
}
