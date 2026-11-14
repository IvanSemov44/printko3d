import { useT } from '../store-settings/useStoreSettings';
import { ORDER_STEPS, statusText, stepIndex } from './orderStatus';

/**
 * Where the order is: the current status in words, and the steps with the one that is now marked for a screen reader.
 * A cancelled order shows no steps, because it does not go on.
 */
export function OrderStatusTrack({ status }: { status: string }) {
  const t = useT();
  const current = stepIndex(status);

  return (
    <section aria-labelledby="my-order-status">
      <h2 id="my-order-status" className="order-status">
        {t('myOrder.statusLabel')} <strong>{t(statusText(status))}</strong>
      </h2>
      {current >= 0 && (
        <ol className="order-track" aria-label={t('myOrder.steps')}>
          {ORDER_STEPS.map((step, index) => (
            <li
              key={step}
              className={index <= current ? 'order-step order-step-done' : 'order-step'}
              aria-current={index === current ? 'step' : undefined}
            >
              {t(statusText(step))}
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
