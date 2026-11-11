import { useCallback, useEffect, useRef, useState, type RefObject } from 'react';
import { useLocation } from 'react-router';

interface RouteChange {
  mainRef: RefObject<HTMLElement | null>;
  /** The heading of the page that was just opened; empty until the user moves to another page. */
  announcement: string;
}

/**
 * The page does not reload when the user follows a link, so a keyboard or screen reader user would stay where the
 * link was. On every change of the path (not the first load, and not `?page=`) this moves the focus to the content
 * and passes the heading of the new page to a live region, once the content is there (it may still be loading).
 */
export function useRouteChange(): RouteChange {
  const { pathname } = useLocation();
  const mainRef = useRef<HTMLElement>(null);
  const [announcement, setAnnouncement] = useState('');
  const shownPath = useRef(pathname);
  const waiting = useRef(false);

  const announce = useCallback(() => {
    if (!waiting.current) {
      return;
    }
    const heading = mainRef.current?.querySelector('h1')?.textContent.trim();
    if (heading) {
      waiting.current = false;
      setAnnouncement(heading);
    }
  }, []);

  useEffect(() => {
    if (shownPath.current === pathname) {
      return;
    }
    shownPath.current = pathname;
    waiting.current = true;
    mainRef.current?.focus();
    // The content of a page that is already loaded is in the document now; the observer below handles the rest.
    queueMicrotask(announce);
  }, [pathname, announce]);

  useEffect(() => {
    const main = mainRef.current;
    if (!main) {
      return;
    }
    const observer = new MutationObserver(announce);
    observer.observe(main, { childList: true, subtree: true, characterData: true });
    return () => {
      observer.disconnect();
    };
  }, [announce]);

  return { mainRef, announcement };
}
