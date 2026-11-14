import { formatMoney, type Order } from '@storefront/shared';
import { t } from '../../i18n';

/** The lines of an order, the delivery and the total. Money is in the currency of the order. */
export function OrderLines({ order, locale }: { order: Order; locale: string }) {
  const money = (amount: number) => formatMoney(amount, order.currency, locale);
  return (
    <div className="table-wrap">
      <table className="product-table">
        <caption className="visually-hidden">{t('orders.items.caption')}</caption>
        <thead>
          <tr>
            <th scope="col">{t('orders.items.product')}</th>
            <th scope="col">{t('orders.items.sku')}</th>
            <th scope="col">{t('orders.items.unitPrice')}</th>
            <th scope="col">{t('orders.items.quantity')}</th>
            <th scope="col">{t('orders.items.lineTotal')}</th>
          </tr>
        </thead>
        <tbody>
          {order.items.map((item) => (
            <tr key={item.sku}>
              <th scope="row">
                <strong>{item.productName}</strong>
                <br />
                <span className="field-hint">{item.variantName}</span>
              </th>
              <td>{item.sku}</td>
              <td>{money(item.unitPrice)}</td>
              <td>{item.quantity}</td>
              <td>{money(item.lineTotal)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <th scope="row" colSpan={4}>
              {t('orders.items.shipping')}
            </th>
            <td>{money(order.shippingAmount)}</td>
          </tr>
          <tr>
            <th scope="row" colSpan={4}>
              {t('orders.items.total')}
            </th>
            <td>
              <strong>{money(order.total)}</strong>
            </td>
          </tr>
        </tfoot>
      </table>
    </div>
  );
}
