import { Link } from 'react-router';
import { useT } from '../store-settings/useStoreSettings';

export function NotFoundPage() {
  const t = useT();
  return (
    <>
      <h1>{t('notFound.heading')}</h1>
      <Link to="/">{t('notFound.back')}</Link>
    </>
  );
}
