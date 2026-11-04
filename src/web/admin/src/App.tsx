import { ApiStatus, useApiHealth } from '@storefront/shared';
import { t } from './i18n';

export function App() {
  const state = useApiHealth();

  return (
    <main>
      <h1>{t('app.title')}</h1>
      <ApiStatus
        state={state}
        labels={{
          checking: t('api.checking'),
          available: t('api.available'),
          unavailable: t('api.unavailable'),
        }}
      />
    </main>
  );
}
