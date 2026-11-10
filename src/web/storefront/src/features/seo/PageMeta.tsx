import { useLocation } from 'react-router';
import { useSiteUrl } from '../store-settings/useRuntimeConfigQuery';
import { useStoreSettings } from '../store-settings/useStoreSettings';
import { canonicalUrl } from './canonical';
import { buildDescription, buildTitle } from './text';

interface PageMetaProps {
  /** The name of the page; the title is `{pageTitle} – {store name}`. Without it the title is the store name. */
  pageTitle?: string;
  description?: string;
  /** The page number of a paged list; the canonical address keeps `?page=` from the second page on. */
  page?: number;
  /** Keep the page out of search results (missing and failed pages). A demo store is always kept out. */
  noindex?: boolean;
  /** Address of the main image for sharing. */
  image?: string | null;
  type?: 'website' | 'product';
}

/** Title, description, canonical address, robots and sharing tags of the current page. React 19 moves them to `<head>`. */
export function PageMeta({
  pageTitle,
  description,
  page = 1,
  noindex = false,
  image = null,
  type = 'website',
}: PageMetaProps) {
  const { storeName, isDemo } = useStoreSettings();
  const siteUrl = useSiteUrl();
  const { pathname } = useLocation();

  const title = buildTitle(storeName, pageTitle);
  const text = description === undefined ? undefined : buildDescription(description);
  const keepOut = noindex || isDemo;
  // A page that is kept out on purpose (missing, failed) has no canonical address. A demo store keeps it.
  const url = noindex ? null : canonicalUrl(siteUrl, pathname, page);

  return (
    <>
      <title>{title}</title>
      {text !== undefined && text !== '' && <meta name="description" content={text} />}
      {url !== null && <link rel="canonical" href={url} />}
      {keepOut && <meta name="robots" content="noindex" />}
      <meta property="og:site_name" content={storeName} />
      <meta property="og:type" content={type} />
      <meta property="og:title" content={title} />
      {text !== undefined && text !== '' && <meta property="og:description" content={text} />}
      {url !== null && <meta property="og:url" content={url} />}
      {image !== null && <meta property="og:image" content={image} />}
      <meta name="twitter:card" content={image === null ? 'summary' : 'summary_large_image'} />
      <meta name="twitter:title" content={title} />
      {text !== undefined && text !== '' && <meta name="twitter:description" content={text} />}
      {image !== null && <meta name="twitter:image" content={image} />}
    </>
  );
}
