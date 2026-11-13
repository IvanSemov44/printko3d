import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { CartNoticesContext } from './CartNoticesContext';
import { findNotices, loadSeen, saveSeen, snapshotOf, type CartNotice } from './cartNotices';
import { useCartQuery } from './useCartQuery';

/**
 * Compares every cart the server gives with the one the buyer saw last, and keeps what changed until it is closed.
 * It sits above the panel and the page, so a change that happens while the panel is shut is still told when it opens.
 */
export function CartNoticesProvider({ children }: { children: ReactNode }) {
  const { data: cart } = useCartQuery();
  const [notices, setNotices] = useState<CartNotice[]>([]);

  useEffect(() => {
    if (!cart) {
      return;
    }
    const previous = loadSeen();
    // The first cart of a tab has nothing to be compared with; what is saved now is the base for the next one.
    const found = previous === null ? [] : findNotices(previous, cart);
    saveSeen(snapshotOf(cart));
    if (found.length > 0) {
      // eslint-disable-next-line react-hooks/set-state-in-effect -- the notices come from comparing with the tab's storage, which is not in React
      setNotices((current) => [
        ...current.filter((notice) => !found.some((added) => added.id === notice.id)),
        ...found,
      ]);
    }
  }, [cart]);

  const dismiss = useCallback((id: string) => {
    setNotices((current) => current.filter((notice) => notice.id !== id));
  }, []);

  const value = useMemo(() => ({ notices, dismiss }), [notices, dismiss]);
  return <CartNoticesContext value={value}>{children}</CartNoticesContext>;
}
