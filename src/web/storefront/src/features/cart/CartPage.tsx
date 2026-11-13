import { formatMoney } from '@storefront/shared';
import { Link } from 'react-router';
import { CatalogError } from '../../components/CatalogError';
import { LoadingStatus } from '../../components/LoadingStatus';
import { PageMeta } from '../seo/PageMeta';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';
import { CartLines } from './CartLines';
import { ClearCartButton } from './ClearCartButton';
import { useCartQuery } from '../../components/useCartQuery';

/** `/cart`. The same lines and actions as the panel, with the sum beside them. Kept out of search results. */
export function CartPage() {
  const t = useT();
  const { defaultCurrency, defaultLocale } = useStoreSettings();
  const { data: cart, isError, refetch } = useCartQuery();

  let content;
  if (!cart) {
    content = isError ? (
      <CatalogError message={t('cart.loadError')} onRetry={() => void refetch()} />
    ) : (
      <LoadingStatus />
    );
  } else if (cart.lines.length === 0) {
    content = (
      <div className="empty-state">
        <p>{t('cart.empty')}</p>
        <Link className="button" to="/">
          {t('cart.emptyAction')}
        </Link>
      </div>
    );
  } else {
    content = (
      <div className="cart-page">
        <div className="cart-page-lines">
          <CartLines cart={cart} />
          <ClearCartButton />
        </div>
        <section className="cart-summary" aria-labelledby="cart-summary-title">
          <h2 id="cart-summary-title">{t('cart.summary')}</h2>
          <p className="cart-subtotal">
            <span>{t('cart.subtotal')}</span>
            <strong>{formatMoney(cart.total, defaultCurrency, defaultLocale)}</strong>
          </p>
          <p className="cart-delivery-note">{t('cart.deliveryNote')}</p>
          <Link className="button" to="/checkout">
            {t('cart.checkout')}
          </Link>
        </section>
      </div>
    );
  }

  return (
    <>
      <PageMeta pageTitle={t('cart.title')} noindex />
      <h1>{t('cart.title')}</h1>
      {content}
    </>
  );
}
