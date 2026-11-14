import { externalLinkAddress, prepareExternalLink, type Customer } from '@storefront/shared';
import { useMutation } from '@tanstack/react-query';
import { useSearchParams } from 'react-router';
import type { MessageKey } from '../../i18n';
import { useT } from '../store-settings/useStoreSettings';
import { navigateAway } from './navigateAway';
import { useExternalProviders } from './useExternalProviders';

/** The text for the `error` that the shop was sent back with after connecting a provider. */
const LINK_ERRORS: Readonly<Record<string, MessageKey>> = {
  already_linked: 'account.external.error.already_linked',
};

/**
 * The social providers that are turned on, with the ones connected to this account marked, and a button for the others (decision 0039).
 * Connecting asks the server first, for a short cookie that proves the buyer wanted it, and then leaves for the provider.
 */
export function ExternalConnections({ customer }: { customer: Customer }) {
  const t = useT();
  const providers = useExternalProviders();
  const [params] = useSearchParams();
  const linkedNow = params.get('linked');
  const error = params.get('error');
  const connect = useMutation({
    mutationFn: (provider: string) => prepareExternalLink(provider),
    onSuccess: (_, provider) => {
      navigateAway(externalLinkAddress(provider));
    },
  });

  if (providers.length === 0) {
    return null;
  }

  const errorKey = error === null ? undefined : LINK_ERRORS[error];
  return (
    <section aria-labelledby="external-title">
      <h2 id="external-title">{t('account.external.title')}</h2>
      {linkedNow !== null && providers.includes(linkedNow) && (
        <p role="status">{t('account.external.linkedNow', { provider: linkedNow })}</p>
      )}
      {errorKey !== undefined && <p role="alert">{t(errorKey)}</p>}
      {connect.isError && <p role="alert">{t('account.error.generic')}</p>}
      <ul className="external-connections">
        {providers.map((provider) => (
          <li key={provider}>
            {customer.linkedProviders.includes(provider) ? (
              <span>{t('account.external.linked', { provider })}</span>
            ) : (
              <button
                type="button"
                className="button button-secondary"
                disabled={connect.isPending}
                onClick={() => {
                  connect.mutate(provider);
                }}
              >
                {t('account.external.link', { provider })}
              </button>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
