import { loginCustomer, registerCustomer } from '@storefront/shared';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type SyntheticEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import { CART_KEY } from '../../components/cartKeys';
import { FormField } from '../../components/FormField';
import { PageMeta } from '../seo/PageMeta';
import { useT } from '../store-settings/useStoreSettings';
import { passwordFailure } from './accountErrors';
import {
  firstInvalid,
  PASSWORD_MIN_LENGTH,
  validateRegistration,
  type AccountErrors,
} from './accountValidation';
import { ExternalSignInButtons } from './ExternalSignInButtons';
import { CUSTOMER_KEY } from './useCustomer';

/**
 * `/account/register`. Makes an account (decision 0039). The answer is the same for an address that already has one, so the
 * page tries to sign the buyer in with what was typed: it works for a new account, and for an old one only with its password.
 */
export function RegisterPage() {
  const t = useT();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [values, setValues] = useState({ name: '', email: '', password: '' });
  const [errors, setErrors] = useState<AccountErrors>({});
  const [done, setDone] = useState(false);
  const register = useMutation({
    mutationFn: async () => {
      const email = values.email.trim();
      await registerCustomer({ email, password: values.password, name: values.name.trim() });
      try {
        return await loginCustomer(email, values.password);
      } catch {
        // Not an error: an address that already has an account, with another password, gets the same answer.
        return null;
      }
    },
    onSuccess: (customer) => {
      if (customer === null) {
        setDone(true);
        return;
      }
      queryClient.setQueryData(CUSTOMER_KEY, customer);
      void queryClient.invalidateQueries({ queryKey: CART_KEY });
      void navigate('/account');
    },
  });

  function change(field: keyof typeof values, value: string) {
    setValues((current) => ({ ...current, [field]: value }));
  }

  function submit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    const found = validateRegistration(values);
    setErrors(found);
    const first = firstInvalid(found, ['name', 'email', 'password']);
    if (first !== undefined) {
      const element = event.currentTarget.elements.namedItem(first);
      if (element instanceof HTMLElement) {
        element.focus();
      }
      return;
    }
    register.mutate();
  }

  if (done) {
    return (
      <>
        <PageMeta pageTitle={t('account.register.title')} noindex />
        <h1>{t('account.register.title')}</h1>
        <p role="status">{t('account.register.done')}</p>
        <Link className="button" to="/account/login">
          {t('account.login.title')}
        </Link>
      </>
    );
  }

  return (
    <>
      <PageMeta pageTitle={t('account.register.title')} noindex />
      <h1>{t('account.register.title')}</h1>
      <form noValidate onSubmit={submit} className="checkout-fields">
        {register.isError && <p role="alert">{t(passwordFailure(register.error))}</p>}
        <FormField
          id="register-name"
          name="name"
          label={t('account.name')}
          error={errors.name === undefined ? undefined : t(errors.name)}
        >
          {(control) => (
            <input
              {...control}
              type="text"
              autoComplete="name"
              value={values.name}
              onChange={(event) => {
                change('name', event.target.value);
              }}
            />
          )}
        </FormField>
        <FormField
          id="register-email"
          name="email"
          label={t('account.email')}
          error={errors.email === undefined ? undefined : t(errors.email)}
        >
          {(control) => (
            <input
              {...control}
              type="email"
              autoComplete="email"
              value={values.email}
              onChange={(event) => {
                change('email', event.target.value);
              }}
            />
          )}
        </FormField>
        <FormField
          id="register-password"
          name="password"
          label={t('account.password')}
          hint={t('account.passwordHint', { count: PASSWORD_MIN_LENGTH })}
          error={errors.password === undefined ? undefined : t(errors.password)}
        >
          {(control) => (
            <input
              {...control}
              type="password"
              autoComplete="new-password"
              value={values.password}
              onChange={(event) => {
                change('password', event.target.value);
              }}
            />
          )}
        </FormField>
        <button type="submit" className="button" disabled={register.isPending}>
          {register.isPending ? t('account.register.submitting') : t('account.register.submit')}
        </button>
      </form>
      <ExternalSignInButtons />
      <p>
        {t('account.register.haveAccount')}{' '}
        <Link to="/account/login">{t('account.login.title')}</Link>
      </p>
    </>
  );
}
