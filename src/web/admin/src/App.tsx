import { LoginForm } from './features/auth/LoginForm';
import { SignedIn } from './features/auth/SignedIn';
import { useCurrentAdmin } from './features/auth/useAdminSession';
import { t } from './i18n';

export function App() {
  const session = useCurrentAdmin();

  // Nothing but the sign-in form is shown until the API has said who is here.
  let content;
  if (session.isPending) {
    content = <p role="status">{t('auth.loading')}</p>;
  } else if (session.isError) {
    content = (
      <>
        <p role="alert" className="form-error">
          {t('auth.error.sessionUnknown')}
        </p>
        <button
          type="button"
          onClick={() => {
            void session.refetch();
          }}
        >
          {t('auth.retry')}
        </button>
      </>
    );
  } else if (session.data === null) {
    content = <LoginForm />;
  } else {
    content = <SignedIn user={session.data} />;
  }

  return (
    <main>
      <h1>{t('app.title')}</h1>
      {content}
    </main>
  );
}
