import { ApiError, confirmCustomerEmail } from '@storefront/shared';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { Link, useSearchParams } from 'react-router';
import { PageMeta } from '../seo/PageMeta';
import { useT } from '../store-settings/useStoreSettings';
import { CUSTOMER_KEY } from './useCustomer';

/**
 * `/account/confirm-email?email=…&token=…`. The link of the confirmation email (decision 0039). The page sends the two values in a
 * body, once, as soon as it opens. The page is kept out of search results.
 */
export function ConfirmEmailPage() {
  const t = useT();
  const queryClient = useQueryClient();
  const [params] = useSearchParams();
  const email = params.get('email') ?? '';
  const token = params.get('token') ?? '';
  const started = useRef(false);
  const confirm = useMutation({
    mutationFn: () => confirmCustomerEmail(email, token),
    onSuccess: () => {
      // A buyer who is signed in has an address that is confirmed now.
      void queryClient.invalidateQueries({ queryKey: CUSTOMER_KEY });
    },
  });
  const { mutate } = confirm;
  const hasLink = email !== '' && token !== '';

  useEffect(() => {
    if (hasLink && !started.current) {
      started.current = true;
      mutate();
    }
  }, [hasLink, mutate]);

  return (
    <>
      <PageMeta pageTitle={t('account.confirm.title')} noindex />
      <h1>{t('account.confirm.title')}</h1>
      {confirm.isSuccess && (
        <>
          <p role="status">{t('account.confirm.done')}</p>
          <Link className="button" to="/account">
            {t('account.title')}
          </Link>
        </>
      )}
      {(confirm.isError || !hasLink) && (
        <p role="alert">
          {confirm.error instanceof ApiError && confirm.error.status !== 422
            ? t('account.error.generic')
            : t('account.confirm.invalid')}
        </p>
      )}
      {(confirm.isIdle || confirm.isPending) && hasLink && (
        <p role="status">{t('account.confirm.working')}</p>
      )}
    </>
  );
}
