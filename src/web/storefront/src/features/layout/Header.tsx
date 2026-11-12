import { Link } from 'react-router';
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
      <CartButton />
    </header>
  );
}
