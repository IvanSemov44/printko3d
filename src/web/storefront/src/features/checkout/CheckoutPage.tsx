import { formatMoney } from '@storefront/shared';
import { useRef, useState, type SyntheticEvent } from 'react';
import { Link } from 'react-router';
import { CatalogError } from '../../components/CatalogError';
import { LoadingStatus } from '../../components/LoadingStatus';
import { useCartQuery } from '../../components/useCartQuery';
import { PageMeta } from '../seo/PageMeta';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';
import { checkoutFailure } from './checkoutErrors';
import {
  CHECKOUT_FIELDS,
  validateCheckout,
  type CheckoutErrors,
  type CheckoutField,
  type CheckoutValues,
} from './checkoutValidation';
import { CheckoutFields } from './CheckoutFields';
import { CheckoutLines } from './CheckoutLines';
import { usePlaceOrder } from './usePlaceOrder';
import { ShippingSection } from './ShippingSection';
import { useShippingQuery } from './useShippingQuery';

const EMPTY_VALUES: CheckoutValues = {
  name: '',
  email: '',
  phone: '',
  street: '',
  city: '',
  postalCode: '',
};

function focusFirst(form: HTMLFormElement, errors: CheckoutErrors) {
  const first = CHECKOUT_FIELDS.find((field) => errors[field] !== undefined);
  const element = first === undefined ? null : form.elements.namedItem(first);
  if (element instanceof HTMLElement) {
    element.focus();
  }
}

function withoutError(errors: CheckoutErrors, field: CheckoutField): CheckoutErrors {
  return Object.fromEntries(Object.entries(errors).filter(([name]) => name !== field));
}

/** What the server refused, as the message under each field it named. */
function marksOf(fields: CheckoutField[]): CheckoutErrors {
  return Object.fromEntries(
    fields.map((field) => [
      field,
      field === 'termsAccepted' ? 'checkout.error.terms' : 'checkout.error.invalidField',
    ]),
  );
}

/** `/checkout`. Contact and address, a look at the order, the terms, and one button that places it. Kept out of search results. */
export function CheckoutPage() {
  const t = useT();
  const { defaultCurrency, defaultLocale } = useStoreSettings();
  const { data: cart, isError, refetch } = useCartQuery();
  const placeOrder = usePlaceOrder();
  const [chosenCode, setChosenCode] = useState<string | null>(null);
  const form = useRef<HTMLFormElement>(null);
  const [values, setValues] = useState<CheckoutValues>(EMPTY_VALUES);
  const [termsAccepted, setTermsAccepted] = useState(false);
  const [errors, setErrors] = useState<CheckoutErrors>({});

  const failure = placeOrder.error ? checkoutFailure(placeOrder.error) : null;
  // A line that cannot be bought stops the order here; the server would refuse it too.
  const hasBlockedLine = cart?.lines.some((line) => line.state !== 'Available') ?? false;
  // The delivery is priced for the lines that can be ordered, so it waits for a cart with something to deliver.
  const shipping = useShippingQuery(cart !== undefined && cart.lines.length > 0 && !hasBlockedLine);
  const methods = shipping.data?.methods ?? [];
  const chosen = methods.find((method) => method.code === chosenCode) ?? methods[0];

  function clearError(field: CheckoutField) {
    setErrors((current) => withoutError(current, field));
  }

  function submit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    if (cart === undefined || chosen === undefined || placeOrder.isPending || hasBlockedLine) {
      return;
    }
    const found = validateCheckout(values, termsAccepted);
    setErrors(found);
    if (Object.keys(found).length > 0) {
      focusFirst(event.currentTarget, found);
      return;
    }
    placeOrder.mutate(
      {
        name: values.name.trim(),
        email: values.email.trim(),
        phone: values.phone.trim(),
        street: values.street.trim(),
        city: values.city.trim(),
        postalCode: values.postalCode.trim(),
        termsAccepted,
        shippingMethod: chosen.code,
        expectedTotal: cart.total + chosen.amount,
      },
      {
        onError: (error) => {
          const refused = checkoutFailure(error);
          if (refused.kind === 'fields') {
            const marked = marksOf(refused.fields);
            setErrors(marked);
            if (form.current) {
              focusFirst(form.current, marked);
            }
          }
        },
      },
    );
  }

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
      <form ref={form} className="checkout-page" noValidate onSubmit={submit}>
        <CheckoutFields
          values={values}
          errors={errors}
          onChange={(field, value) => {
            setValues((current) => ({ ...current, [field]: value }));
            clearError(field);
          }}
        />
        <section className="checkout-review" aria-labelledby="checkout-review-title">
          <h2 id="checkout-review-title">{t('checkout.review')}</h2>
          {cart.lines.some((line) => line.fulfilmentType === 'MadeToOrder') && (
            <p className="cart-made-to-order-note">{t('cart.madeToOrderNote')}</p>
          )}
          <CheckoutLines cart={cart} />
          <ShippingSection query={shipping} chosen={chosen} onChoose={setChosenCode} />
          <p className="cart-subtotal">
            <span>{t('checkout.itemsTotal')}</span>
            <span>{formatMoney(cart.total, defaultCurrency, defaultLocale)}</span>
          </p>
          {chosen !== undefined && (
            <p className="cart-subtotal">
              <span>{t('checkout.shippingTotal')}</span>
              <span>{formatMoney(chosen.amount, defaultCurrency, defaultLocale)}</span>
            </p>
          )}
          <p className="cart-subtotal checkout-total">
            <span>{t('checkout.total')}</span>
            <strong>
              {formatMoney(cart.total + (chosen?.amount ?? 0), defaultCurrency, defaultLocale)}
            </strong>
          </p>
          {hasBlockedLine && (
            <p className="checkout-blocked">
              {t('checkout.blockedLine')} <Link to="/cart">{t('checkout.toCart')}</Link>
            </p>
          )}
          {failure?.kind === 'notice' && (
            <div className="checkout-notice" role="alert">
              <p>{t(failure.message)}</p>
              {failure.toCart && <Link to="/cart">{t('checkout.toCart')}</Link>}
            </div>
          )}
          <div className="field field-check">
            <input
              id="checkout-terms"
              name="termsAccepted"
              type="checkbox"
              checked={termsAccepted}
              aria-invalid={errors.termsAccepted ? true : undefined}
              aria-describedby={errors.termsAccepted ? 'checkout-terms-error' : undefined}
              onChange={(event) => {
                setTermsAccepted(event.target.checked);
                clearError('termsAccepted');
              }}
            />
            <label htmlFor="checkout-terms">{t('checkout.terms')}</label>
            {errors.termsAccepted && (
              <p id="checkout-terms-error" className="field-error">
                {t(errors.termsAccepted)}
              </p>
            )}
          </div>
          <button
            type="submit"
            aria-disabled={placeOrder.isPending || hasBlockedLine || chosen === undefined}
          >
            {placeOrder.isPending ? t('checkout.submitting') : t('checkout.submit')}
          </button>
        </section>
      </form>
    );
  }

  return (
    <>
      <PageMeta pageTitle={t('checkout.title')} noindex />
      <h1>{t('checkout.title')}</h1>
      {content}
    </>
  );
}
