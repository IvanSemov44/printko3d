import { ApiStatus, useApiHealth, type AdminUser } from '@storefront/shared';
import { lazy, Suspense } from 'react';
import { Link, Navigate, Route, Routes } from 'react-router';
import { AdminLayout } from '../../components/AdminLayout';
import { PageLoadBoundary } from '../../components/PageLoadBoundary';
import { t } from '../../i18n';
import { useLogout } from './useAdminSession';

// Each group of pages is its own file, loaded when the first page of the group is opened.
const ProductListPage = lazy(() =>
  import('../products/ProductListPage').then((m) => ({ default: m.ProductListPage })),
);
const ProductFormPage = lazy(() =>
  import('../products/ProductFormPage').then((m) => ({ default: m.ProductFormPage })),
);
const CategoryListPage = lazy(() =>
  import('../categories/CategoryListPage').then((m) => ({ default: m.CategoryListPage })),
);
const CategoryFormPage = lazy(() =>
  import('../categories/CategoryFormPage').then((m) => ({ default: m.CategoryFormPage })),
);
const SettingsPage = lazy(() =>
  import('../settings/SettingsPage').then((m) => ({ default: m.SettingsPage })),
);

const OrderListPage = lazy(() =>
  import('../orders/OrderListPage').then((m) => ({ default: m.OrderListPage })),
);
const OrderDetailPage = lazy(() =>
  import('../orders/OrderDetailPage').then((m) => ({ default: m.OrderDetailPage })),
);

const ShippingTariffPage = lazy(() =>
  import('../shipping/ShippingTariffPage').then((m) => ({ default: m.ShippingTariffPage })),
);

/** What the signed-in administrator sees: who is signed in, the menu and the page of the address. */
export function SignedIn({ user }: { user: AdminUser }) {
  const apiState = useApiHealth();
  const logout = useLogout();

  const footer = (
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

  return (
    <AdminLayout footer={footer}>
      <PageLoadBoundary>
        <Suspense fallback={<p role="status">{t('auth.loading')}</p>}>
          <Routes>
            <Route index element={<Navigate to="/categories" replace />} />
            <Route path="products" element={<ProductListPage />} />
            <Route path="products/new" element={<ProductFormPage />} />
            <Route path="products/:id" element={<ProductFormPage />} />
            <Route path="categories" element={<CategoryListPage />} />
            <Route path="categories/new" element={<CategoryFormPage />} />
            <Route path="categories/:id" element={<CategoryFormPage />} />
            <Route path="orders" element={<OrderListPage />} />
            <Route path="orders/:number" element={<OrderDetailPage />} />
            <Route path="shipping" element={<ShippingTariffPage />} />
            <Route path="settings" element={<SettingsPage />} />
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
        </Suspense>
      </PageLoadBoundary>
    </AdminLayout>
  );
}
