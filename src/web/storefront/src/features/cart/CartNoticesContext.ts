import { createContext } from 'react';
import type { CartNotice } from './cartNotices';

export interface CartNotices {
  /** What changed for the buyer and has not been closed yet. */
  notices: CartNotice[];
  dismiss: (id: string) => void;
}

export const CartNoticesContext = createContext<CartNotices | null>(null);
