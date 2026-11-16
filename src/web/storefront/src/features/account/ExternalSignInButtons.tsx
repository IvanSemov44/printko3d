import { externalSignInAddress } from '@storefront/shared';
import { useT } from '../store-settings/useStoreSettings';
import { useExternalProviders } from './useExternalProviders';

/**
 * One button for each social provider that is turned on (decision 0039). A button is a link, because the browser leaves for the provider
 * and comes back; it is not a request. Nothing is shown when no provider is on.
 */
export function ExternalSignInButtons() {
  const t = useT();
  const providers = useExternalProviders();
  if (providers.length === 0) {
    return null;
  }

  return (
    <div className="external-sign-in">
      <p className="external-sign-in-divider">{t('account.external.or')}</p>
      <ul className="external-sign-in-list">
        {providers.map((provider) => (
          <li key={provider}>
            <a className="button button-secondary" href={externalSignInAddress(provider)}>
              {t('account.external.signInWith', { provider })}
            </a>
          </li>
        ))}
      </ul>
    </div>
  );
}
