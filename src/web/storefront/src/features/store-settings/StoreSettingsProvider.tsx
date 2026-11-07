import { useEffect, type ReactNode } from 'react';
import { t } from '../../i18n';
import { StoreSettingsContext } from './StoreSettingsContext';
import { applyTheme } from './theme';
import { useStoreSettingsQuery } from './useStoreSettingsQuery';

interface StoreSettingsProviderProps {
  children: ReactNode;
}

export function StoreSettingsProvider({ children }: StoreSettingsProviderProps) {
  const { data, isError, isFetching, refetch } = useStoreSettingsQuery();

  useEffect(() => {
    if (data) {
      document.title = data.storeName;
      applyTheme(data.primaryColor);
    }
  }, [data]);

  if (data) {
    return <StoreSettingsContext value={data}>{children}</StoreSettingsContext>;
  }

  if (isError && !isFetching) {
    return (
      <main className="status-screen" role="alert">
        <p>{t('settings.error')}</p>
        <button type="button" onClick={() => void refetch()}>
          {t('settings.retry')}
        </button>
      </main>
    );
  }

  return (
    <main className="status-screen" role="status">
      {t('settings.loading')}
    </main>
  );
}
