import {
  ApiError,
  formatMoney,
  type ShippingOptions,
  type ShippingQuote,
} from '@storefront/shared';
import type { UseQueryResult } from '@tanstack/react-query';
import { Link } from 'react-router';
import { CatalogError } from '../../components/CatalogError';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';
import { shippingMethodNameKey } from './shippingMethodName';

interface ShippingSectionProps {
  query: UseQueryResult<ShippingOptions>;
  /** The method the buyer chose; the first one until they choose. */
  chosen: ShippingQuote | undefined;
  onChoose: (code: string) => void;
}

/** The delivery of the cart: the methods with their price, one chosen. Nothing is chosen until the price is known. */
export function ShippingSection({ query, chosen, onChoose }: ShippingSectionProps) {
  const t = useT();
  const { defaultCurrency, defaultLocale } = useStoreSettings();

  let content;
  if (query.isError) {
    content =
      query.error instanceof ApiError && query.error.code === 'shipping.too_heavy' ? (
        <p className="checkout-notice" role="alert">
          {t('shipping.tooHeavy')} <Link to="/cart">{t('checkout.toCart')}</Link>
        </p>
      ) : (
        <CatalogError message={t('shipping.loadError')} onRetry={() => void query.refetch()} />
      );
  } else if (!query.data) {
    content = <p role="status">{t('shipping.loading')}</p>;
  } else if (query.data.methods.length === 0) {
    content = <p className="checkout-notice">{t('shipping.none')}</p>;
  } else {
    content = query.data.methods.map((method) => {
      const id = `shipping-${method.code}`;
      return (
        <div key={method.code} className="field field-check">
          <input
            id={id}
            type="radio"
            name="shippingMethod"
            checked={chosen?.code === method.code}
            onChange={() => {
              onChoose(method.code);
            }}
          />
          <label htmlFor={id}>
            {t(shippingMethodNameKey(method.code))}{' '}
            <strong>{formatMoney(method.amount, defaultCurrency, defaultLocale)}</strong>
          </label>
        </div>
      );
    });
  }

  return (
    <fieldset className="checkout-shipping">
      <legend>{t('shipping.title')}</legend>
      {content}
    </fieldset>
  );
}
