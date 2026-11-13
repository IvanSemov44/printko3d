import { useT } from '../features/store-settings/useStoreSettings';

/** The loading text for the content area, announced politely by screen readers. */
export function LoadingStatus() {
  const t = useT();
  return <p role="status">{t('settings.loading')}</p>;
}
