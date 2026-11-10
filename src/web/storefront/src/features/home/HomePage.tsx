import { PageMeta } from '../seo/PageMeta';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';

export function HomePage() {
  const t = useT();
  const { storeName } = useStoreSettings();
  return (
    <>
      <PageMeta description={t('seo.homeDescription', { storeName })} />
      <h1>{t('home.heading')}</h1>
    </>
  );
}
