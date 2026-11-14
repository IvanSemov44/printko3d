import {
  changeCustomerPassword,
  logoutCustomer,
  resendCustomerConfirmation,
  type Customer,
} from '@storefront/shared';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState, type SyntheticEvent } from 'react';
import { Navigate, useNavigate } from 'react-router';
import { CART_KEY } from '../../components/cartKeys';
import { FormField } from '../../components/FormField';
import { LoadingStatus } from '../../components/LoadingStatus';
import { PageMeta } from '../seo/PageMeta';
import { useT } from '../store-settings/useStoreSettings';
import { passwordFailure } from './accountErrors';
import { firstInvalid, validatePasswordChange, type AccountErrors } from './accountValidation';
import { CUSTOMER_KEY, useCustomer } from './useCustomer';

function PasswordForm() {
  const t = useT();
  const [currentPassword, setCurrentPassword] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<AccountErrors>({});
  const change = useMutation({
    mutationFn: () => changeCustomerPassword({ currentPassword, newPassword: password }),
    onSuccess: () => {
      setCurrentPassword('');
      setPassword('');
    },
  });

  function submit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault();
    change.reset();
    const found = validatePasswordChange({ currentPassword, password });
    setErrors(found);
    const first = firstInvalid(found, ['currentPassword', 'password']);
    if (first !== undefined) {
      const element = event.currentTarget.elements.namedItem(first);
      if (element instanceof HTMLElement) {
        element.focus();
      }
      return;
    }
    change.mutate();
  }

  return (
    <form noValidate onSubmit={submit} className="checkout-fields" aria-labelledby="password-title">
      <h2 id="password-title">{t('account.password.title')}</h2>
      {change.isError && <p role="alert">{t(passwordFailure(change.error))}</p>}
      {change.isSuccess && <p role="status">{t('account.password.changed')}</p>}
      <FormField
        id="current-password"
        name="currentPassword"
        label={t('account.password.current')}
        error={errors.currentPassword === undefined ? undefined : t(errors.currentPassword)}
      >
        {(control) => (
          <input
            {...control}
            type="password"
            autoComplete="current-password"
            value={currentPassword}
            onChange={(event) => {
              setCurrentPassword(event.target.value);
            }}
          />
        )}
      </FormField>
      <FormField
        id="new-password"
        name="password"
        label={t('account.password.new')}
        error={errors.password === undefined ? undefined : t(errors.password)}
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
      <button type="submit" className="button" disabled={change.isPending}>
        {change.isPending ? t('account.password.saving') : t('account.password.save')}
      </button>
    </form>
  );
}

function ResendConfirmation() {
  const t = useT();
  const resend = useMutation({ mutationFn: () => resendCustomerConfirmation() });

  return (
    <div>
      <p role="status">
        {resend.isSuccess ? t('account.confirm.sent') : t('account.emailNotConfirmed')}
      </p>
      {resend.isError && <p role="alert">{t('account.error.generic')}</p>}
      {!resend.isSuccess && (
        <button
          type="button"
          className="button button-secondary"
          disabled={resend.isPending}
          onClick={() => {
            resend.mutate();
          }}
        >
          {t('account.confirm.resend')}
        </button>
      )}
    </div>
  );
}

function Profile({ customer }: { customer: Customer }) {
  const t = useT();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const signedOut = useRef(false);
  const signOut = useMutation({
    mutationFn: () => logoutCustomer(),
    onSettled: () => {
      // The buyer is out also when the server could not be told: the cookie is the server's, and it ends on its own.
      signedOut.current = true;
      void navigate('/');
      void queryClient.invalidateQueries({ queryKey: CART_KEY });
    },
  });

  // The known buyer is cleared when this page is gone: cleared sooner, the page would send the visitor to the sign-in
  // instead of the home page.
  useEffect(
    () => () => {
      if (signedOut.current) {
        queryClient.setQueryData(CUSTOMER_KEY, null);
      }
    },
    [queryClient],
  );

  return (
    <>
      <p>
        {t('account.signedInAs')} <strong>{customer.displayName}</strong> ({customer.email})
      </p>
      {!customer.emailConfirmed && <ResendConfirmation />}
      {customer.hasPassword ? <PasswordForm /> : <p>{t('account.noPassword')}</p>}
      <button
        type="button"
        className="button button-secondary"
        disabled={signOut.isPending}
        onClick={() => {
          signOut.mutate();
        }}
      >
        {t('account.logout')}
      </button>
    </>
  );
}

/** `/account`. The profile of the signed-in buyer (decision 0039). Without a session the buyer goes to the sign-in. */
export function AccountPage() {
  const t = useT();
  const { data, isPending, isError } = useCustomer();

  if (isPending) {
    return <LoadingStatus />;
  }
  if (isError) {
    return <p role="alert">{t('account.error.generic')}</p>;
  }
  if (data === null) {
    return <Navigate to="/account/login" replace />;
  }
  return (
    <>
      <PageMeta pageTitle={t('account.title')} noindex />
      <h1>{t('account.title')}</h1>
      <Profile customer={data} />
    </>
  );
}
