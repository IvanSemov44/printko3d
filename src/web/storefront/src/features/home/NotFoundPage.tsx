import { Link } from 'react-router';
import { PageMeta } from '../seo/PageMeta';
import { useT } from '../store-settings/useStoreSettings';

/** Shown for an unknown address and for a missing product or category. Not for the index: the server still answers 200. */
export function NotFoundPage() {
  const t = useT();
  return (
    <>
      <PageMeta pageTitle={t('notFound.heading')} noindex />
      <h1>{t('notFound.heading')}</h1>
      <Link to="/">{t('notFound.back')}</Link>
    </>
  );
}
