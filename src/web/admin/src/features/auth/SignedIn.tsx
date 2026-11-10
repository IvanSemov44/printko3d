import { ApiStatus, useApiHealth, type AdminUser } from '@storefront/shared';
import { Link, Navigate, NavLink, Route, Routes } from 'react-router';
import { t } from '../../i18n';
import { CategoryFormPage } from '../categories/CategoryFormPage';
import { CategoryListPage } from '../categories/CategoryListPage';
import { ProductFormPage } from '../products/ProductFormPage';
import { ProductListPage } from '../products/ProductListPage';
import { useLogout } from './useAdminSession';

/** What the signed-in administrator sees: who is signed in, the menu and the page of the address. */
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

      <nav aria-label={t('nav.label')}>
        <NavLink to="/products">{t('nav.products')}</NavLink>
        <NavLink to="/categories">{t('nav.categories')}</NavLink>
      </nav>

      <Routes>
        <Route index element={<Navigate to="/categories" replace />} />
        <Route path="products" element={<ProductListPage />} />
        <Route path="products/new" element={<ProductFormPage />} />
        <Route path="products/:id" element={<ProductFormPage />} />
        <Route path="categories" element={<CategoryListPage />} />
        <Route path="categories/new" element={<CategoryFormPage />} />
        <Route path="categories/:id" element={<CategoryFormPage />} />
        <Route
          path="*"
          element={
            <>
              <p>{t('nav.notFound')}</p>
              <Link to="/categories">{t('categories.backToList')}</Link>
            </>
          }
        />
      </Routes>
    </>
  );
}
