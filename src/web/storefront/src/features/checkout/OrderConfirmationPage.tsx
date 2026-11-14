import { formatMoney, readOrder, type Order } from '@storefront/shared';
import { Link, useLocation, useParams } from 'react-router';
import { describeAvailability } from '../../components/availability';
import { PageMeta } from '../seo/PageMeta';
import { shippingMethodNameKey } from './shippingMethodName';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';

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

/** `/order/:number`. What was ordered and that the payment is not there yet. Kept out of search results. */
export function OrderConfirmationPage() {
  const t = useT();
  const { defaultLocale } = useStoreSettings();
  const { number } = useParams();
  const order = orderFromState(useLocation().state, number);

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

  const money = (amount: number) => formatMoney(amount, order.currency, defaultLocale);
  return (
    <>
      <PageMeta pageTitle={t('order.title')} noindex />
      <h1>{t('order.title')}</h1>
      <p className="order-number">
        {t('order.numberLabel')} <strong>{`PR-${String(order.number)}`}</strong>
      </p>
      <p className="checkout-notice">{t('order.paymentNote')}</p>
      <div className="checkout-page">
        <section className="checkout-fields" aria-labelledby="order-items-title">
          <h2 id="order-items-title">{t('order.items')}</h2>
          <ul className="order-lines">
            {order.items.map((item) => (
              <li key={`${item.sku}-${String(item.quantity)}`} className="order-line">
                <div>
                  <p className="cart-line-name">{item.productName}</p>
                  <p className="cart-line-variant">{item.variantName}</p>
                  {item.fulfilmentType === 'MadeToOrder' && (
                    <p className="cart-line-availability">
                      {describeAvailability(t, defaultLocale, {
                        fulfilmentType: item.fulfilmentType,
                        isAvailable: true,
                        leadTimeDays: item.leadTimeDays,
                      })}
                    </p>
                  )}
                  <p className="cart-line-price">
                    {t('checkout.lineQuantity', {
                      quantity: String(item.quantity),
                      price: money(item.unitPrice),
                    })}
                  </p>
                </div>
                <p className="cart-line-total">{money(item.lineTotal)}</p>
              </li>
            ))}
          </ul>
          {order.shippingMethod !== '' && (
            <p className="cart-subtotal">
              <span>{t(shippingMethodNameKey(order.shippingMethod))}</span>
              <span>{money(order.shippingAmount)}</span>
            </p>
          )}
          <p className="cart-subtotal checkout-total">
            <span>{t('checkout.total')}</span>
            <strong>{money(order.total)}</strong>
          </p>
        </section>
        <section className="checkout-review" aria-labelledby="order-contact-title">
          <h2 id="order-contact-title">{t('order.contact')}</h2>
          <p>{order.customerName}</p>
          <p>{order.shippingStreet}</p>
          <p>{`${order.shippingPostalCode} ${order.shippingCity}`}</p>
          <p>{order.email}</p>
          <p>{order.phone}</p>
          <Link className="button" to="/">
            {t('order.toStart')}
          </Link>
        </section>
      </div>
    </>
  );
}
