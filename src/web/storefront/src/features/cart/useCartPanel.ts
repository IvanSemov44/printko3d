import { useContext } from 'react';
import { CartPanelContext, type CartPanel } from './CartPanelContext';

export function useCartPanel(): CartPanel {
  const panel = useContext(CartPanelContext);
  if (!panel) {
    throw new Error('useCartPanel must be used inside CartPanelProvider');
  }
  return panel;
}
