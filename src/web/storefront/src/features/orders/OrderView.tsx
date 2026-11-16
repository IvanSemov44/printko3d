import type { Order } from '@storefront/shared';
import { OrderDetails } from '../checkout/OrderDetails';
import { useT } from '../store-settings/useStoreSettings';
import { OrderStatusTrack } from './OrderStatusTrack';

/** An order for its buyer: the number, the steps it goes through and what is in it. Used by "My order" and by the account. */
export function OrderView({ order }: { order: Order }) {
  const t = useT();
  return (
    <>
      <p className="order-number">
        {t('order.numberLabel')} <strong>{`PR-${String(order.number)}`}</strong>
      </p>
      <OrderStatusTrack status={order.status} />
      <OrderDetails order={order} />
    </>
  );
}
