import { ApiError } from '@storefront/shared';
import { useRef, useState, type SubmitEvent } from 'react';
import { t, type MessageKey } from '../../i18n';
import { validateLogin, type LoginErrors } from './loginValidation';
import { useLogin } from './useAdminSession';

/** The same text for a wrong email, a wrong password and a locked account: the API does not say which. */
function messageFor(error: Error): MessageKey {
  if (error instanceof ApiError) {
    if (error.status === 401) {
      return 'auth.error.invalid';
    }
    if (error.status === 429) {
      return 'auth.error.tooMany';
    }
  }
  return 'auth.error.unavailable';
}

function field(data: FormData, name: string): string {
  const value = data.get(name);
  return typeof value === 'string' ? value : '';
}

export function LoginForm() {
  const login = useLogin();
  const [errors, setErrors] = useState<LoginErrors>({});
  const emailInput = useRef<HTMLInputElement>(null);
  const passwordInput = useRef<HTMLInputElement>(null);

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const values = { email: field(data, 'email').trim(), password: field(data, 'password') };

    const found = validateLogin(values);
    setErrors(found);
    if (found.email) {
      emailInput.current?.focus();
      return;
    }
    if (found.password) {
      passwordInput.current?.focus();
      return;
    }

    login.mutate(values, {
      onError: () => {
        // Do not keep the wrong password in the page.
        if (passwordInput.current) {
          passwordInput.current.value = '';
          passwordInput.current.focus();
        }
      },
    });
  }

  return (
    <form onSubmit={handleSubmit} noValidate aria-labelledby="login-title">
      <h2 id="login-title">{t('auth.title')}</h2>
      {login.error ? (
        <p role="alert" className="form-error">
          {t(messageFor(login.error))}
        </p>
      ) : null}
      <div className="field">
        <label htmlFor="login-email">{t('auth.email')}</label>
        <input
          ref={emailInput}
          id="login-email"
          name="email"
          type="email"
          autoComplete="username"
          aria-invalid={errors.email ? true : undefined}
          aria-describedby={errors.email ? 'login-email-error' : undefined}
        />
        {errors.email ? (
          <p id="login-email-error" className="field-error">
            {t(errors.email)}
          </p>
        ) : null}
      </div>
      <div className="field">
        <label htmlFor="login-password">{t('auth.password')}</label>
        <input
          ref={passwordInput}
          id="login-password"
          name="password"
          type="password"
          autoComplete="current-password"
          aria-invalid={errors.password ? true : undefined}
          aria-describedby={errors.password ? 'login-password-error' : undefined}
        />
        {errors.password ? (
          <p id="login-password-error" className="field-error">
            {t(errors.password)}
          </p>
        ) : null}
      </div>
      <button type="submit" disabled={login.isPending}>
        {login.isPending ? t('auth.submitting') : t('auth.submit')}
      </button>
    </form>
  );
}
