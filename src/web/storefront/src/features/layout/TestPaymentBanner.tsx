import { useT } from '../store-settings/useStoreSettings';
import { usePaymentMode } from './usePaymentMode';

/**
 * Shown on every page while the payment (and with it the delivery) is a test one (decision 0035). The server says so, so
 * the notice goes away by itself when a real provider replaces the test one. No close button on purpose.
 */
export function TestPaymentBanner() {
  const t = useT();
  const { data } = usePaymentMode();

  if (data?.isTest !== true) {
    return null;
  }
  return (
    <aside className="demo-banner" aria-label={t('payment.testBannerLabel')}>
      {t('payment.testBanner')}
    </aside>
  );
}
