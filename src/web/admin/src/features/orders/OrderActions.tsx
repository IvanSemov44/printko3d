import { ApiError, type AdminOrder, type OrderStatus } from '@storefront/shared';
import { useState } from 'react';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { t } from '../../i18n';
import { ORDER_STEP_KEYS, orderNumberText } from './orderLabels';
import { useCancelOrder, useChangeOrderStatus } from './useOrders';

function isConflict(error: unknown): boolean {
  return error instanceof ApiError && error.status === 409;
}

/**
 * What the administrator can do with the order now: the next production step and the refusal. The server decides what
 * is allowed (`nextStatuses`, `canCancel`); the buttons only show it. A refusal of a paid order says that the money goes
 * back by hand, because the system cannot return it (decision 0037).
 */
export function OrderActions({ data }: { data: AdminOrder }) {
  const { order } = data;
  const change = useChangeOrderStatus(order.number);
  const cancel = useCancelOrder(order.number);
  const [confirming, setConfirming] = useState(false);
  const [refundByHand, setRefundByHand] = useState<boolean | null>(null);
  const paid = order.status !== 'New';

  function step(status: OrderStatus) {
    setRefundByHand(null);
    change.mutate(status);
  }

  function refuse() {
    cancel.mutate(undefined, {
      onSuccess: (cancelled) => {
        setConfirming(false);
        setRefundByHand(cancelled.refundByHand);
      },
      onError: () => {
        setConfirming(false);
      },
    });
  }

  const failed = change.isError ? change.error : cancel.isError ? cancel.error : null;

  return (
    <>
      {refundByHand !== null ? (
        <div role="status">
          <p>{t('orders.cancelled')}</p>
          {refundByHand ? <p className="form-error">{t('orders.refundByHand')}</p> : null}
        </div>
      ) : null}

      {failed ? (
        <p role="alert" className="form-error">
          {isConflict(failed) ? t('orders.error.changed') : t('orders.error.actionFailed')}
        </p>
      ) : null}

      {data.nextStatuses.length === 0 && !data.canCancel && refundByHand === null ? (
        <p>{t('orders.noActions')}</p>
      ) : null}

      <div className="form-actions">
        {data.nextStatuses.map((status) => {
          const key = ORDER_STEP_KEYS[status];
          return key ? (
            <button
              key={status}
              type="button"
              disabled={change.isPending}
              onClick={() => {
                step(status);
              }}
            >
              {t(key)}
            </button>
          ) : null;
        })}
        {data.canCancel ? (
          <button
            type="button"
            className="danger"
            onClick={() => {
              setConfirming(true);
            }}
          >
            {t('orders.cancel')}
          </button>
        ) : null}
      </div>
      {data.nextStatuses.includes('Shipped') ? (
        <p className="field-hint">{t('orders.step.shippedHint')}</p>
      ) : null}

      <ConfirmDialog
        open={confirming}
        title={t('orders.cancelTitle', { number: orderNumberText(order.number) })}
        confirmLabel={t('orders.cancelConfirm')}
        cancelLabel={t('orders.cancelBack')}
        busy={cancel.isPending}
        onConfirm={refuse}
        onCancel={() => {
          setConfirming(false);
        }}
      >
        <p>{paid ? t('orders.cancelBodyPaid') : t('orders.cancelBody')}</p>
      </ConfirmDialog>
    </>
  );
}
