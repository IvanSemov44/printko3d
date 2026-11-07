import { useStoreSettings, useT } from '../store-settings/useStoreSettings';

/** Shown on every page of a demo store. It has no close button on purpose. */
export function DemoBanner() {
  const { isDemo } = useStoreSettings();
  const t = useT();

  if (!isDemo) {
    return null;
  }
  return (
    <aside className="demo-banner" aria-label={t('demo.bannerLabel')}>
      {t('demo.banner')}
    </aside>
  );
}
