import { Link } from 'react-router';
import { useCategoriesQuery } from '../catalog/useCategoriesQuery';
import { PageMeta } from '../seo/PageMeta';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';
import { CategoryPreview } from './CategoryPreview';

const MAX_SECTIONS = 4;

export function HomePage() {
  const t = useT();
  const { storeName } = useStoreSettings();
  const { data } = useCategoriesQuery();
  const topLevel = data?.filter((category) => category.parentId === null) ?? [];
  const first = topLevel[0];

  return (
    <>
      <PageMeta description={t('seo.homeDescription', { storeName })} />
      <section className="hero" aria-labelledby="home-title">
        <p className="label hero-eyebrow">{storeName}</p>
        <h1 id="home-title">{t('home.heading')}</h1>
        <p className="hero-text">{t('home.intro')}</p>
        {first ? (
          <Link to={`/categories/${first.slug}`} className="button">
            {t('home.browse')}
          </Link>
        ) : null}
      </section>
      {topLevel.slice(0, MAX_SECTIONS).map((category) => (
        <CategoryPreview key={category.id} category={category} />
      ))}
    </>
  );
}
