import { Outlet } from 'react-router';
import { DemoBanner } from './DemoBanner';
import { Footer } from './Footer';
import { Header } from './Header';
import { MAIN_CONTENT_ID, SkipLink } from './SkipLink';

export function Layout() {
  return (
    <>
      <SkipLink />
      <DemoBanner />
      <Header />
      <main id={MAIN_CONTENT_ID} tabIndex={-1}>
        <Outlet />
      </main>
      <Footer />
    </>
  );
}
