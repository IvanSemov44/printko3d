import {
  completeTestPayment,
  readOrder,
  type Order,
  type TestPaymentOutcome,
} from '@storefront/shared';
import { useMutation } from '@tanstack/react-query';
import { Link, useLocation, useNavigate, useParams } from 'react-router';
import { PageMeta } from '../seo/PageMeta';
import { useT } from '../store-settings/useStoreSettings';
import { completeTestFailure } from './paymentFailure';

interface ReturnTarget {
  order: Order;
  paymentId: string;
}

/** What the order page put in the state of the route, if it is whole. A fresh visit of this address has nothing. */
function targetFromState(state: unknown): ReturnTarget | null {
  if (
    typeof state !== 'object' ||
    state === null ||
    !('order' in state) ||
    !('paymentId' in state) ||
    typeof state.paymentId !== 'string'
  ) {
    return null;
  }
  try {
    return { order: readOrder(state.order), paymentId: state.paymentId };
  } catch {
    return null;
  }
}

/**
 * `/pay/test/:reference`. The page of the test provider (decision 0035): there is no card to type and no money to take,
 * only a choice of how the payment ends. The choice goes to the server, which signs a message like a real provider and
 * handles it; the buyer is then led back to the order. Kept out of search results.
 */
export function TestPaymentPage() {
  const t = useT();
  const { reference = '' } = useParams();
  const target = targetFromState(useLocation().state);
  const navigate = useNavigate();
  const complete = useMutation({
    mutationFn: (outcome: TestPaymentOutcome) => completeTestPayment(reference, outcome),
    onSuccess: () => {
      if (target === null) {
        void navigate('/');
      } else {
        void navigate(`/order/${String(target.order.number)}`, { state: target });
      }
    },
  });

  return (
    <>
      <PageMeta pageTitle={t('payment.testPage.title')} noindex />
      <h1>{t('payment.testPage.title')}</h1>
      <p className="checkout-notice">{t('payment.testPage.note')}</p>
      {complete.isError && <p role="alert">{t(completeTestFailure(complete.error))}</p>}
      <div className="payment-actions">
        <button
          type="button"
          className="button"
          disabled={complete.isPending}
          onClick={() => {
            complete.mutate('Paid');
          }}
        >
          {t('payment.testPage.succeed')}
        </button>
        <button
          type="button"
          className="button button-secondary"
          disabled={complete.isPending}
          onClick={() => {
            complete.mutate('Failed');
          }}
        >
          {t('payment.testPage.fail')}
        </button>
      </div>
      {target !== null && (
        <p>
          <Link to={`/order/${String(target.order.number)}`} state={target}>
            {t('payment.testPage.back')}
          </Link>
        </p>
      )}
    </>
  );
}
