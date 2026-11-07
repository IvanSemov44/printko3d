import { useT } from '../store-settings/useStoreSettings';

export const MAIN_CONTENT_ID = 'content';

export function SkipLink() {
  const t = useT();
  return (
    <a className="skip-link" href={`#${MAIN_CONTENT_ID}`}>
      {t('layout.skipToContent')}
    </a>
  );
}
