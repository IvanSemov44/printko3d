import { useEffect, useState, type ReactNode } from 'react';
import { NavLink } from 'react-router';
import { t, type MessageKey } from '../i18n';
import { useStoreMoney } from './useStoreMoney';

/** The screens of the menu, in the order of docs/design.md section 11. Only screens that exist are listed. */
const MENU: readonly { to: string; label: MessageKey }[] = [
  { to: '/orders', label: 'nav.orders' },
  { to: '/products', label: 'nav.products' },
  { to: '/categories', label: 'nav.categories' },
  { to: '/shipping', label: 'nav.shipping' },
  { to: '/settings', label: 'nav.settings' },
];

/**
 * The frame of every signed-in page: the side panel (a top bar with a menu button below 1024px), who is signed in
 * at its foot, and the content. The name in the panel is the name of the shop, not a name written in the code.
 */
export function AdminLayout({ footer, children }: { footer: ReactNode; children: ReactNode }) {
  const [menuOpen, setMenuOpen] = useState(false);
  const money = useStoreMoney();
  const storeName = money.data?.storeName ?? '';

  useEffect(() => {
    if (!menuOpen) {
      return;
    }
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setMenuOpen(false);
      }
    }
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [menuOpen]);

  return (
    <div className="admin-shell">
      <div className="admin-topbar">
        <button
          type="button"
          className="secondary"
          aria-expanded={menuOpen}
          aria-controls="admin-sidebar"
          onClick={() => {
            setMenuOpen((open) => !open);
          }}
        >
          {t('nav.menu')}
        </button>
        <span className="admin-wordmark">{storeName}</span>
      </div>

      <aside id="admin-sidebar" className="admin-sidebar" data-open={menuOpen}>
        <div className="admin-brand">
          <span className="admin-wordmark">{storeName}</span>
          <span className="admin-brand-label">{t('app.adminLabel')}</span>
        </div>
        <nav aria-label={t('nav.label')}>
          {MENU.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              onClick={() => {
                setMenuOpen(false);
              }}
            >
              {t(item.label)}
            </NavLink>
          ))}
        </nav>
        <div className="admin-sidebar-foot">{footer}</div>
      </aside>

      <main className="admin-content">
        <h1 className="visually-hidden">{t('app.title')}</h1>
        {children}
      </main>
    </div>
  );
}
