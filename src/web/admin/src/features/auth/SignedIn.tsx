import { ApiStatus, useApiHealth, type AdminUser } from '@storefront/shared';
import { t } from '../../i18n';
import { useLogout } from './useAdminSession';

/** What the signed-in administrator sees. The admin functions (categories, products) come with later tasks. */
export function SignedIn({ user }: { user: AdminUser }) {
  const apiState = useApiHealth();
  const logout = useLogout();

  return (
    <>
      <p>
        {t('auth.signedInAs')} <strong>{user.email}</strong>
      </p>
      <ApiStatus
        state={apiState}
        labels={{
          checking: t('api.checking'),
          available: t('api.available'),
          unavailable: t('api.unavailable'),
        }}
      />
      {logout.isError ? (
        <p role="alert" className="form-error">
          {t('auth.error.logoutFailed')}
        </p>
      ) : null}
      <button
        type="button"
        onClick={() => {
          logout.mutate();
        }}
        disabled={logout.isPending}
      >
        {t('auth.logout')}
      </button>
    </>
  );
}
