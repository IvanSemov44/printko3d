import { formatMoney, type Order } from '@storefront/shared';
import { Link } from 'react-router';
import { describeAvailability } from '../../components/availability';
import { shippingMethodNameKey } from './shippingMethodName';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';

/** What was ordered, the delivery, the total and the contact of an order. Shared by the confirmation and the page "My order". */
export function OrderDetails({ order }: { order: Order }) {
  const t = useT();
  const { defaultLocale } = useStoreSettings();
  const money = (amount: number) => formatMoney(amount, order.currency, defaultLocale);
  return (
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
  );
}
