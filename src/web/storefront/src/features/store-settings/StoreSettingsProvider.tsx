import { useEffect, type ReactNode } from 'react';
import { t } from '../../i18n';
import { StoreSettingsContext } from './StoreSettingsContext';
import { applyTheme } from './theme';
import { useRuntimeConfigQuery } from './useRuntimeConfigQuery';
import { useStoreSettingsQuery } from './useStoreSettingsQuery';

interface StoreSettingsProviderProps {
  children: ReactNode;
}

export function StoreSettingsProvider({ children }: StoreSettingsProviderProps) {
  const settings = useStoreSettingsQuery();
  const config = useRuntimeConfigQuery();
  const { data } = settings;
  const isError = settings.isError || config.isError;
  const isFetching = settings.isFetching || config.isFetching;
  const refetch = () => Promise.all([settings.refetch(), config.refetch()]);

  useEffect(() => {
    if (data) {
      applyTheme(data.primaryColor);
    }
  }, [data]);

  if (data && config.data) {
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
