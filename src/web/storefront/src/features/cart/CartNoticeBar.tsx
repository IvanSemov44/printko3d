import { formatMoney } from '@storefront/shared';
import type { MessageKey } from '../../i18n';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';
import type { CartNotice } from './cartNotices';
import { useCartNotices } from './useCartNotices';

const STATE_KEYS = {
  InsufficientStock: 'cart.notice.insufficientStock',
  Inactive: 'cart.notice.unavailable',
  Archived: 'cart.notice.unavailable',
} as const satisfies Record<Extract<CartNotice, { kind: 'state' }>['state'], MessageKey>;

/**
 * What changed in the cart since the buyer last saw it: a price, or a line that can no longer be bought.
 * The region is always there, so a screen reader announces a notice that arrives later. Both prices are the server's.
 */
export function CartNoticeBar() {
  const t = useT();
  const { defaultCurrency, defaultLocale } = useStoreSettings();
  const { notices, dismiss } = useCartNotices();

  return (
    <div role="status" className="cart-notices">
      {notices.map((notice) => (
        <div key={notice.id} className="cart-notice">
          <p>
            {notice.kind === 'price'
              ? t('cart.notice.priceChanged', {
                  name: notice.productName,
                  from: formatMoney(notice.from, defaultCurrency, defaultLocale),
                  to: formatMoney(notice.to, defaultCurrency, defaultLocale),
                })
              : t(STATE_KEYS[notice.state], { name: notice.productName })}
          </p>
          <button
            type="button"
            className="button-secondary"
            aria-label={t('cart.notice.dismiss', { name: notice.productName })}
            onClick={() => {
              dismiss(notice.id);
            }}
          >
            <span aria-hidden="true">×</span>
          </button>
        </div>
      ))}
    </div>
  );
}
