/**
 * The address of a stored image (decision 0011): the base address plus the storage key.
 * Returns `null` when the environment has no image storage, so callers show the placeholder.
 */
export function imageUrl(baseUrl: string | null, storageKey: string): string | null {
  const segments = storageKey.split('/').filter((segment) => segment !== '');
  if (baseUrl === null || segments.length === 0) {
    return null;
  }
  return `${baseUrl}/${segments.map(encodeURIComponent).join('/')}`;
}
