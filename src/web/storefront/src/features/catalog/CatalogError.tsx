import { useT } from '../store-settings/useStoreSettings';

interface CatalogErrorProps {
  /** Text for the failure; the product list text by default. */
  message?: string;
  onRetry: () => void;
}

/** A general message with a retry button; no details from the failure are shown. */
export function CatalogError({ message, onRetry }: CatalogErrorProps) {
  const t = useT();
  return (
    <div role="alert">
      <p>{message ?? t('catalog.error')}</p>
      <button type="button" onClick={onRetry}>
        {t('settings.retry')}
      </button>
    </div>
  );
}
