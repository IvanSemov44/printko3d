import { useContext } from 'react';
import { CartNoticesContext, type CartNotices } from './CartNoticesContext';

/** The notices of the cart. One source for the panel and the page, so the same change is never told twice. */
export function useCartNotices(): CartNotices {
  const value = useContext(CartNoticesContext);
  if (!value) {
    throw new Error('useCartNotices must be used inside CartNoticesProvider');
  }
  return value;
}
