import { lazy, Suspense } from 'react';
import { useCartPanel } from './useCartPanel';

const CartPanel = lazy(() =>
  import('./CartPanel').then((module) => ({ default: module.CartPanel })),
);

/** Loads the panel the first time somebody opens it, so a visitor who never opens it never downloads it. */
export function CartPanelHost() {
  const { hasOpened } = useCartPanel();
  if (!hasOpened) {
    return null;
  }
  return (
    <Suspense fallback={null}>
      <CartPanel />
    </Suspense>
  );
}
