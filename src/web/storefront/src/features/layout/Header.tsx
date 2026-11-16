import { Link } from 'react-router';
import { AccountLink } from '../account/AccountLink';
import { CartButton } from '../cart/CartButton';
import { useStoreSettings } from '../store-settings/useStoreSettings';
import { CategoryNav } from './CategoryNav';

export function Header() {
  const { storeName } = useStoreSettings();
  return (
    <header className="site-header">
      <Link className="site-name" to="/">
        {storeName}
      </Link>
      <CategoryNav />
      <AccountLink />
      <CartButton />
    </header>
  );
}
