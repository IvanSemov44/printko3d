import { createContext } from 'react';

export interface CartPanel {
  isOpen: boolean;
  /** True once the panel was opened, so the code of the panel is loaded only for a visitor who needs it. */
  hasOpened: boolean;
  open: () => void;
  close: () => void;
  /** Puts the focus back on the control that opened the panel. The panel calls it after the dialog is closed. */
  returnFocus: () => void;
}

export const CartPanelContext = createContext<CartPanel | null>(null);
