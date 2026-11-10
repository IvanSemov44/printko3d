/**
 * The address search engines should index: origin and path, no query except `?page=` from the second page on.
 * A trailing slash is dropped (except for the home page) so `/products/a/` and `/products/a` are one page.
 */
export function canonicalUrl(origin: string, pathname: string, page = 1): string {
  const path = pathname.length > 1 ? pathname.replace(/\/+$/, '') : pathname;
  const query = page > 1 ? `?page=${String(page)}` : '';
  return `${origin}${path === '' ? '/' : path}${query}`;
}
