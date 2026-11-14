import { ORDER_STATUSES, type OrderStatus, type OrderStatusCounts } from '@storefront/shared';
import { t } from '../../i18n';
import { ORDER_STATUS_KEYS } from './orderLabels';

/** The statuses as pills with the number of orders in each. The number is left out until it is known. */
export function OrderStatusFilter({
  selected,
  counts,
  onSelect,
}: {
  selected: OrderStatus | undefined;
  counts: OrderStatusCounts | undefined;
  onSelect: (status: OrderStatus | undefined) => void;
}) {
  const total = counts ? Object.values(counts).reduce((sum, count) => sum + count, 0) : undefined;

  function pill(status: OrderStatus | undefined, label: string, count: number | undefined) {
    return (
      <button
        key={status ?? 'all'}
        type="button"
        className="filter-pill"
        aria-pressed={selected === status}
        onClick={() => {
          onSelect(status);
        }}
      >
        {label}
        {count === undefined ? null : (
          <>
            {' '}
            <span className="filter-count">{count}</span>
          </>
        )}
      </button>
    );
  }

  return (
    <div className="filter-pills" role="group" aria-label={t('orders.filters.status')}>
      {pill(undefined, t('orders.filters.allStatuses'), total)}
      {ORDER_STATUSES.map((status) => pill(status, t(ORDER_STATUS_KEYS[status]), counts?.[status]))}
    </div>
  );
}
