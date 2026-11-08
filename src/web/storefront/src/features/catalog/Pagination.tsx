import { Link } from 'react-router';
import { useT } from '../store-settings/useStoreSettings';
import { getPageItems } from './pageParams';

interface PaginationProps {
  currentPage: number;
  totalPages: number;
}

function pageLink(page: number) {
  return { search: page === 1 ? '' : `?page=${String(page)}` };
}

/** Shown only when there is more than one page. The first page has no `page` parameter. */
export function Pagination({ currentPage, totalPages }: PaginationProps) {
  const t = useT();
  if (totalPages <= 1) {
    return null;
  }

  return (
    <nav aria-label={t('catalog.pagination')}>
      <ul className="pagination">
        <li>
          {currentPage > 1 ? (
            <Link to={pageLink(currentPage - 1)}>{t('catalog.previous')}</Link>
          ) : (
            <span aria-disabled="true">{t('catalog.previous')}</span>
          )}
        </li>
        {getPageItems(currentPage, totalPages).map((item, index) => (
          <li key={item === 'gap' ? `gap-${String(index)}` : item}>
            {item === 'gap' ? (
              <span aria-hidden="true">…</span>
            ) : (
              <Link
                to={pageLink(item)}
                aria-current={item === currentPage ? 'page' : undefined}
                aria-label={`${t('catalog.page')} ${String(item)}`}
              >
                {item}
              </Link>
            )}
          </li>
        ))}
        <li>
          {currentPage < totalPages ? (
            <Link to={pageLink(currentPage + 1)}>{t('catalog.next')}</Link>
          ) : (
            <span aria-disabled="true">{t('catalog.next')}</span>
          )}
        </li>
      </ul>
    </nav>
  );
}
