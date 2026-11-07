import { useT } from '../store-settings/useStoreSettings';

export function HomePage() {
  const t = useT();
  return <h1>{t('home.heading')}</h1>;
}
