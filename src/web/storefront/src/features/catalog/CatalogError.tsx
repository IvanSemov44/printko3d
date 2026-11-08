import { useT } from '../store-settings/useStoreSettings';

/** A general message with a retry button; no details from the failure are shown. */
export function CatalogError({ onRetry }: { onRetry: () => void }) {
  const t = useT();
  return (
    <div role="alert">
      <p>{t('catalog.error')}</p>
      <button type="button" onClick={onRetry}>
        {t('settings.retry')}
      </button>
    </div>
  );
}
