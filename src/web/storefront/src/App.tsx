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

// The checkout is read by a buyer who is about to order, like the cart page, so it is loaded when it is opened.
const CheckoutPage = lazy(() =>
  import('./features/checkout/CheckoutPage').then((module) => ({ default: module.CheckoutPage })),
);

// The page of the test payment is opened by the buyer who pressed "Pay", so it is loaded when it is opened too.
const TestPaymentPage = lazy(() =>
  import('./features/checkout/TestPaymentPage').then((module) => ({
    default: module.TestPaymentPage,
  })),
);

// The confirmation is read once, right after the checkout, so it is loaded when it is opened too.
const OrderConfirmationPage = lazy(() =>
  import('./features/checkout/OrderConfirmationPage').then((module) => ({
    default: module.OrderConfirmationPage,
  })),
);

// The page "My order" is opened from a link in an email, so it is loaded when it is opened.
const MyOrderPage = lazy(() =>
  import('./features/orders/MyOrderPage').then((module) => ({ default: module.MyOrderPage })),
);

// The account pages are read by a buyer who has chosen to sign in, so they are loaded when they are opened (decision 0039).
const LoginPage = lazy(() =>
  import('./features/account/LoginPage').then((module) => ({ default: module.LoginPage })),
);
const RegisterPage = lazy(() =>
  import('./features/account/RegisterPage').then((module) => ({ default: module.RegisterPage })),
);
const AccountPage = lazy(() =>
  import('./features/account/AccountPage').then((module) => ({ default: module.AccountPage })),
);

const ConfirmEmailPage = lazy(() =>
  import('./features/account/ConfirmEmailPage').then((module) => ({
    default: module.ConfirmEmailPage,
  })),
);
const ForgotPasswordPage = lazy(() =>
  import('./features/account/ForgotPasswordPage').then((module) => ({
    default: module.ForgotPasswordPage,
  })),
);
const ResetPasswordPage = lazy(() =>
  import('./features/account/ResetPasswordPage').then((module) => ({
    default: module.ResetPasswordPage,
  })),
);

const AccountOrdersPage = lazy(() =>
  import('./features/account/AccountOrdersPage').then((module) => ({
    default: module.AccountOrdersPage,
  })),
);
const AccountOrderPage = lazy(() =>
  import('./features/account/AccountOrderPage').then((module) => ({
    default: module.AccountOrderPage,
  })),
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
          <Route
            path="checkout"
            element={
              <Suspense fallback={<LoadingStatus />}>
                <CheckoutPage />
              </Suspense>
            }
          />
          <Route
            path="order/:number"
            element={
              <Suspense fallback={<LoadingStatus />}>
                <OrderConfirmationPage />
              </Suspense>
            }
          />
          <Route
            path="account"
            element={
              <Suspense fallback={<LoadingStatus />}>
                <AccountPage />
              </Suspense>
            }
          />
          <Route
            path="account/orders"
            element={
              <Suspense fallback={<LoadingStatus />}>
                <AccountOrdersPage />
              </Suspense>
            }
          />
          <Route
            path="account/orders/:number"
            element={
              <Suspense fallback={<LoadingStatus />}>
                <AccountOrderPage />
              </Suspense>
            }
          />
          <Route
            path="account/login"
            element={
              <Suspense fallback={<LoadingStatus />}>
                <LoginPage />
              </Suspense>
            }
          />
          <Route
            path="account/register"
            element={
              <Suspense fallback={<LoadingStatus />}>
                <RegisterPage />
              </Suspense>
            }
          />
          <Route
            path="account/confirm-email"
            element={
              <Suspense fallback={<LoadingStatus />}>
                <ConfirmEmailPage />
              </Suspense>
            }
          />
          <Route
            path="account/forgot-password"
            element={
              <Suspense fallback={<LoadingStatus />}>
                <ForgotPasswordPage />
              </Suspense>
            }
          />
          <Route
            path="account/reset-password"
            element={
              <Suspense fallback={<LoadingStatus />}>
                <ResetPasswordPage />
              </Suspense>
            }
          />
          <Route
            path="my-order"
            element={
              <Suspense fallback={<LoadingStatus />}>
                <MyOrderPage />
              </Suspense>
            }
          />
          <Route
            path="pay/test/:reference"
            element={
              <Suspense fallback={<LoadingStatus />}>
                <TestPaymentPage />
              </Suspense>
            }
          />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </StoreSettingsProvider>
  );
}
