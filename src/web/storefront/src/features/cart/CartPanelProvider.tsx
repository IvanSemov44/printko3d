import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react';
import { CartPanelContext } from './CartPanelContext';

/** Shared by the cart button in the header and the button on the product page. Goes inside the layout. */
export function CartPanelProvider({ children }: { children: ReactNode }) {
  const [isOpen, setIsOpen] = useState(false);
  const [hasOpened, setHasOpened] = useState(false);
  const opener = useRef<HTMLElement | null>(null);

  const open = useCallback(() => {
    opener.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    setIsOpen(true);
    setHasOpened(true);
  }, []);

  const close = useCallback(() => {
    setIsOpen(false);
  }, []);

  const returnFocus = useCallback(() => {
    // So a keyboard user does not start over. Called after the dialog is closed: a control behind a modal cannot take focus.
    opener.current?.focus();
    opener.current = null;
  }, []);

  const value = useMemo(
    () => ({ isOpen, hasOpened, open, close, returnFocus }),
    [isOpen, hasOpened, open, close, returnFocus],
  );
  return <CartPanelContext value={value}>{children}</CartPanelContext>;
}
