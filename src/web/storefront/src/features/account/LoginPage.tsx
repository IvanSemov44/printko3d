import { loginCustomer } from '@storefront/shared';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type SyntheticEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import { CART_KEY } from '../../components/cartKeys';
import { FormField } from '../../components/FormField';
import { PageMeta } from '../seo/PageMeta';
import { useT } from '../store-settings/useStoreSettings';
import { loginFailure } from './accountErrors';
import { firstInvalid, validateLogin, type AccountErrors } from './accountValidation';
import { CUSTOMER_KEY } from './useCustomer';

/** `/account/login`. Signs the buyer in with an email and a password (decision 0039). The page is kept out of search results. */
export function LoginPage() {
  const t = useT();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<AccountErrors>({});
  const signIn = useMutation({
    mutationFn: () => loginCustomer(email.trim(), password),
    onSuccess: (customer) => {
      queryClient.setQueryData(CUSTOMER_KEY, customer);
      // The cart of the account is merged with this one when the buyer signs in, so read it again.
      void queryClient.invalidateQueries({ queryKey: CART_KEY });
      void navigate('/account');
    },
  });

  function submit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    const found = validateLogin({ email, password });
    setErrors(found);
    const first = firstInvalid(found, ['email', 'password']);
    if (first !== undefined) {
      const element = event.currentTarget.elements.namedItem(first);
      if (element instanceof HTMLElement) {
        element.focus();
      }
      return;
    }
    signIn.mutate();
  }

  return (
    <>
      <PageMeta pageTitle={t('account.login.title')} noindex />
      <h1>{t('account.login.title')}</h1>
      <form noValidate onSubmit={submit} className="checkout-fields">
        {signIn.isError && <p role="alert">{t(loginFailure(signIn.error))}</p>}
        <FormField
          id="login-email"
          name="email"
          label={t('account.email')}
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
        <FormField
          id="login-password"
          name="password"
          label={t('account.password')}
          error={errors.password === undefined ? undefined : t(errors.password)}
        >
          {(control) => (
            <input
              {...control}
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => {
                setPassword(event.target.value);
              }}
            />
          )}
        </FormField>
        <button type="submit" className="button" disabled={signIn.isPending}>
          {signIn.isPending ? t('account.login.submitting') : t('account.login.submit')}
        </button>
      </form>
      <p>
        <Link to="/account/forgot-password">{t('account.forgot.title')}</Link>
      </p>
      <p>
        {t('account.login.noAccount')}{' '}
        <Link to="/account/register">{t('account.register.title')}</Link>
      </p>
    </>
  );
}
