import { Outlet } from 'react-router';
import { CartPanelHost } from '../cart/CartPanelHost';
import { CartNoticesProvider } from '../cart/CartNoticesProvider';
import { CartPanelProvider } from '../cart/CartPanelProvider';
import { DemoBanner } from './DemoBanner';
import { Footer } from './Footer';
import { Header } from './Header';
import { MAIN_CONTENT_ID, SkipLink } from './SkipLink';
import { useRouteChange } from './useRouteChange';

export function Layout() {
  const { mainRef, announcement } = useRouteChange();
  return (
    <CartNoticesProvider>
      <CartPanelProvider>
        <SkipLink />
        <DemoBanner />
        <Header />
        <main id={MAIN_CONTENT_ID} ref={mainRef} tabIndex={-1}>
          <Outlet />
          {/* Inside <main>, so that the text is not content outside every landmark. */}
          <div
            className="visually-hidden"
            aria-live="polite"
            aria-atomic="true"
            data-testid="route-announcement"
          >
            {announcement}
          </div>
        </main>
        <Footer />
        <CartPanelHost />
      </CartPanelProvider>
    </CartNoticesProvider>
  );
}
