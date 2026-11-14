import type { Order, PaymentState } from '@storefront/shared';
import type { MessageKey } from '../../i18n';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';
import { formatDeadline } from './paymentDeadline';
import { startPaymentFailure } from './paymentFailure';
import { usePaymentState, useStartPayment } from './usePayment';

/** What each state of the payment tells the buyer. */
const STATE_TEXT: Record<PaymentState, MessageKey> = {
  Pending: 'payment.waiting',
  Succeeded: 'payment.succeeded',
  Failed: 'payment.failed',
  Expired: 'payment.expired',
  PaidWithoutOrder: 'payment.paidWithoutOrder',
};

interface PaymentSectionProps {
  order: Order;
  /** The payment the buyer is coming back from, or null when nothing was started yet. */
  paymentId: string | null;
}

/** Paying the order: the button, the last moment to pay, and what the provider said. No card is typed here (rule 9). */
export function PaymentSection({ order, paymentId }: PaymentSectionProps) {
  const t = useT();
  const { defaultLocale } = useStoreSettings();
  const state = usePaymentState(paymentId).data ?? null;
  const start = useStartPayment(order);

  const canPay = state === null || state === 'Pending' || state === 'Failed';
  const deadline =
    canPay && order.paymentDueAt !== null ? formatDeadline(order.paymentDueAt, defaultLocale) : '';
  let buttonKey: MessageKey = 'payment.pay';
  if (state === 'Failed') {
    buttonKey = 'payment.payAgain';
  } else if (state === 'Pending') {
    buttonKey = 'payment.continue';
  }

  return (
    <section className="payment-section" aria-labelledby="payment-title">
      <h2 id="payment-title">{t('payment.title')}</h2>
      {state !== null && <p role="status">{t(STATE_TEXT[state])}</p>}
      {deadline !== '' && <p>{t('payment.dueNote', { deadline })}</p>}
      {start.isError && <p role="alert">{t(startPaymentFailure(start.error))}</p>}
      {canPay && (
        <button
          type="button"
          className="button"
          disabled={start.isPending}
          onClick={() => {
            start.mutate();
          }}
        >
          {t(buttonKey)}
        </button>
      )}
    </section>
  );
}
