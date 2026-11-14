import { ApiError, resetCustomerPassword } from '@storefront/shared';
import { useMutation } from '@tanstack/react-query';
import { useState, type SyntheticEvent } from 'react';
import { Link, useSearchParams } from 'react-router';
import { FormField } from '../../components/FormField';
import { PageMeta } from '../seo/PageMeta';
import { useT } from '../store-settings/useStoreSettings';
import { passwordFailure } from './accountErrors';
import { PASSWORD_MIN_LENGTH } from './accountValidation';

/**
 * `/account/reset-password?email=…&token=…`. The link of the email for a new password (decision 0039). The link works once. The
 * page does not sign the buyer in: it leads to the sign-in. The page is kept out of search results.
 */
export function ResetPasswordPage() {
  const t = useT();
  const [params] = useSearchParams();
  const email = params.get('email') ?? '';
  const token = params.get('token') ?? '';
  const [password, setPassword] = useState('');
  const [tooShort, setTooShort] = useState(false);
  const reset = useMutation({
    mutationFn: () => resetCustomerPassword({ email, token, newPassword: password }),
  });

  function submit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    const short = password.length < PASSWORD_MIN_LENGTH;
    setTooShort(short);
    if (short) {
      const element = event.currentTarget.elements.namedItem('password');
      if (element instanceof HTMLElement) {
        element.focus();
      }
      return;
    }
    reset.mutate();
  }

  const linkProblem =
    email === '' ||
    token === '' ||
    (reset.error instanceof ApiError && reset.error.code === 'account.token_invalid');

  return (
    <>
      <PageMeta pageTitle={t('account.reset.title')} noindex />
      <h1>{t('account.reset.title')}</h1>
      {reset.isSuccess ? (
        <>
          <p role="status">{t('account.reset.done')}</p>
          <Link className="button" to="/account/login">
            {t('account.login.title')}
          </Link>
        </>
      ) : linkProblem ? (
        <>
          <p role="alert">{t('account.reset.invalid')}</p>
          <Link className="button" to="/account/forgot-password">
            {t('account.forgot.title')}
          </Link>
        </>
      ) : (
        <form noValidate onSubmit={submit} className="checkout-fields">
          {reset.isError && <p role="alert">{t(passwordFailure(reset.error))}</p>}
          <FormField
            id="reset-password"
            name="password"
            label={t('account.password.new')}
            hint={t('account.passwordHint', { count: PASSWORD_MIN_LENGTH })}
            error={tooShort ? t('account.error.passwordTooShort') : undefined}
          >
            {(control) => (
              <input
                {...control}
                type="password"
                autoComplete="new-password"
                value={password}
                onChange={(event) => {
                  setPassword(event.target.value);
                }}
              />
            )}
          </FormField>
          <button type="submit" className="button" disabled={reset.isPending}>
            {reset.isPending ? t('account.password.saving') : t('account.reset.submit')}
          </button>
        </form>
      )}
    </>
  );
}
