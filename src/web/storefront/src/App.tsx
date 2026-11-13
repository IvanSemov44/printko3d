import { lazy, Suspense } from 'react';
import { Route, Routes } from 'react-router';
import { CategoryPage } from './features/catalog/CategoryPage';
import { HomePage } from './features/home/HomePage';
import { NotFoundPage } from './components/NotFoundPage';
import { Layout } from './features/layout/Layout';
import { LoadingStatus } from './components/LoadingStatus';
import { ProductPage } from './features/product/ProductPage';
import { StoreSettingsProvider } from './features/store-settings/StoreSettingsProvider';

// The cart page is read by a buyer who has something in the cart, not by every visitor, so it is loaded when it is opened.
const CartPage = lazy(() =>
  import('./features/cart/CartPage').then((module) => ({ default: module.CartPage })),
);

/** Needs a router and a QueryClientProvider above it (see main.tsx). */
export function App() {
  return (
    <StoreSettingsProvider>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<HomePage />} />
          <Route path="categories/:slug" element={<CategoryPage />} />
          <Route path="products/:slug" element={<ProductPage />} />
          <Route
            path="cart"
            element={
              <Suspense fallback={<LoadingStatus />}>
                <CartPage />
              </Suspense>
            }
          />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </StoreSettingsProvider>
  );
}
