import { requestPasswordReset } from '@storefront/shared';
import { useMutation } from '@tanstack/react-query';
import { useState, type SyntheticEvent } from 'react';
import { Link } from 'react-router';
import { FormField } from '../../components/FormField';
import { PageMeta } from '../seo/PageMeta';
import { useT } from '../store-settings/useStoreSettings';
import { passwordFailure } from './accountErrors';
import { looksLikeEmail } from './accountValidation';

/**
 * `/account/forgot-password`. Asks for the link for a new password (decision 0039). The answer is the same for an address that has
 * an account and one that has not, so the page never says which it was. The page is kept out of search results.
 */
export function ForgotPasswordPage() {
  const t = useT();
  const [email, setEmail] = useState('');
  const [invalid, setInvalid] = useState(false);
  const ask = useMutation({ mutationFn: () => requestPasswordReset(email.trim()) });

  function submit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    const bad = !looksLikeEmail(email);
    setInvalid(bad);
    if (bad) {
      const element = event.currentTarget.elements.namedItem('email');
      if (element instanceof HTMLElement) {
        element.focus();
      }
      return;
    }
    ask.mutate();
  }

  return (
    <>
      <PageMeta pageTitle={t('account.forgot.title')} noindex />
      <h1>{t('account.forgot.title')}</h1>
      {ask.isSuccess ? (
        <>
          <p role="status">{t('account.forgot.done')}</p>
          <Link className="button" to="/account/login">
            {t('account.login.title')}
          </Link>
        </>
      ) : (
        <form noValidate onSubmit={submit} className="checkout-fields">
          <p>{t('account.forgot.intro')}</p>
          {ask.isError && <p role="alert">{t(passwordFailure(ask.error))}</p>}
          <FormField
            id="forgot-email"
            name="email"
            label={t('account.email')}
            error={invalid ? t('account.error.emailInvalid') : undefined}
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
          <button type="submit" className="button" disabled={ask.isPending}>
            {ask.isPending ? t('account.forgot.sending') : t('account.forgot.submit')}
          </button>
        </form>
      )}
    </>
  );
}
