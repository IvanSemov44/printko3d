import { Link } from 'react-router';
import { useT } from '../store-settings/useStoreSettings';
import { useCustomer } from './useCustomer';

/** The link in the header: the way in for a visitor, the account for a signed-in buyer. Nothing while the answer is not known. */
export function AccountLink() {
  const t = useT();
  const { data, isPending } = useCustomer();
  if (isPending) {
    return <span className="account-link" aria-hidden="true" />;
  }
  return (
    <Link className="account-link" to={data ? '/account' : '/account/login'}>
      {data ? t('account.link.account') : t('account.link.login')}
    </Link>
  );
}
