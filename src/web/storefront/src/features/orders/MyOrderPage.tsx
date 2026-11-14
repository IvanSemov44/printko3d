import { findOrder, findOrderByToken, type Order } from '@storefront/shared';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useState, type SyntheticEvent } from 'react';
import { Link, useSearchParams } from 'react-router';
import { FormField } from '../../components/FormField';
import { OrderDetails } from '../checkout/OrderDetails';
import { PageMeta } from '../seo/PageMeta';
import { useT } from '../store-settings/useStoreSettings';
import { looksLikeEmail, myOrderFailure, parseOrderNumber } from './myOrderFailure';
import { OrderStatusTrack } from './OrderStatusTrack';

type FieldError = 'myOrder.error.numberInvalid' | 'myOrder.error.emailInvalid';

function OrderView({ order }: { order: Order }) {
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

function LookupForm({ onFound }: { onFound: (order: Order) => void }) {
  const t = useT();
  const [number, setNumber] = useState('');
  const [email, setEmail] = useState('');
  const [errors, setErrors] = useState<{ number?: FieldError; email?: FieldError }>({});
  const search = useMutation({
    mutationFn: ({ orderNumber, address }: { orderNumber: number; address: string }) =>
      findOrder(orderNumber, address.trim()),
    onSuccess: onFound,
  });

  function submit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    const orderNumber = parseOrderNumber(number);
    const found = {
      ...(orderNumber === null ? { number: 'myOrder.error.numberInvalid' as const } : {}),
      ...(looksLikeEmail(email) ? {} : { email: 'myOrder.error.emailInvalid' as const }),
    };
    setErrors(found);
    if (orderNumber === null || found.email !== undefined) {
      const first = orderNumber === null ? 'number' : 'email';
      const element = event.currentTarget.elements.namedItem(first);
      if (element instanceof HTMLElement) {
        element.focus();
      }
      return;
    }
    search.mutate({ orderNumber, address: email });
  }

  return (
    <form noValidate onSubmit={submit} className="checkout-fields">
      <p>{t('myOrder.intro')}</p>
      {search.isError && <p role="alert">{t(myOrderFailure(search.error))}</p>}
      <FormField
        id="my-order-number"
        name="number"
        label={t('myOrder.number')}
        hint={t('myOrder.numberHint')}
        error={errors.number === undefined ? undefined : t(errors.number)}
      >
        {(control) => (
          <input
            {...control}
            type="text"
            inputMode="numeric"
            autoComplete="off"
            value={number}
            onChange={(event) => {
              setNumber(event.target.value);
            }}
          />
        )}
      </FormField>
      <FormField
        id="my-order-email"
        name="email"
        label={t('myOrder.email')}
        error={errors.email === undefined ? undefined : t(errors.email)}
      >
        {(control) => (
          <input
            {...control}
            type="email"
            autoComplete="email"
            value={email}
            onChange={(event) => {
              setEmail(event.target.value);
            }}
          />
        )}
      </FormField>
      <button type="submit" className="button" disabled={search.isPending}>
        {search.isPending ? t('myOrder.searching') : t('myOrder.submit')}
      </button>
    </form>
  );
}

/** The order that the key of the link in an email belongs to. A key that is wrong sends the buyer to the form. */
function TokenOrder({ token }: { token: string }) {
  const t = useT();
  const { data, isError, isPending } = useQuery({
    queryKey: ['my-order', 'token', token],
    queryFn: ({ signal }) => findOrderByToken(token, signal),
    retry: false,
    // The key is a secret in an address: the answer is not kept for a later visit.
    gcTime: 0,
  });

  if (isPending) {
    return <p role="status">{t('myOrder.loading')}</p>;
  }
  if (isError) {
    return (
      <>
        <p role="alert">{t('myOrder.error.badLink')}</p>
        <Link className="button" to="/my-order">
          {t('myOrder.title')}
        </Link>
      </>
    );
  }
  return <OrderView order={data} />;
}

/**
 * `/my-order`. A buyer without an account sees the order again (decision 0036): by the key in the link of the email
 * (`?token=`), or by typing the number and the email. The page is kept out of search results, and the answer is
 * kept only in this page.
 */
export function MyOrderPage() {
  const t = useT();
  const [params] = useSearchParams();
  const token = params.get('token');
  const [found, setFound] = useState<Order | null>(null);

  return (
    <>
      <PageMeta pageTitle={t('myOrder.title')} noindex />
      <h1>{t('myOrder.title')}</h1>
      {token !== null && token !== '' ? (
        <TokenOrder token={token} />
      ) : found === null ? (
        <LookupForm onFound={setFound} />
      ) : (
        <>
          <OrderView order={found} />
          <button
            type="button"
            className="button"
            onClick={() => {
              setFound(null);
            }}
          >
            {t('myOrder.another')}
          </button>
        </>
      )}
    </>
  );
}
