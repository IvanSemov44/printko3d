import { Route, Routes } from 'react-router';
import { HomePage } from './features/home/HomePage';
import { NotFoundPage } from './features/home/NotFoundPage';
import { Layout } from './features/layout/Layout';
import { StoreSettingsProvider } from './features/store-settings/StoreSettingsProvider';

/** Needs a router and a QueryClientProvider above it (see main.tsx). */
export function App() {
  return (
    <StoreSettingsProvider>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<HomePage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </StoreSettingsProvider>
  );
}
