import {
  field,
  readArray,
  readBoolean,
  readInteger,
  readObject,
  readOneOf,
  readString,
  type Reader,
} from './decode';
import { apiRequest, apiSend } from './http';
import { ORDER_STATUSES, readOrder, type Order, type OrderStatus } from './orders';

const ORDERS_PATH = '/api/v1/admin/orders';

/** One row of the admin list of orders. Money is in the smallest unit of `currency`. */
export interface AdminOrderListItem {
  number: number;
  status: OrderStatus;
  currency: string;
  total: number;
  customerName: string;
  email: string;
  itemCount: number;
  createdAt: string;
}

export interface AdminOrderPage {
  items: AdminOrderListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/** What the list is filtered by. Empty or missing means no filter. */
export interface AdminOrderQuery {
  /** One or more statuses; an order shows if it has any of them. */
  status?: readonly OrderStatus[];
  /** The exact order number, or part of the email or the name. */
  search?: string;
  page: number;
  pageSize: number;
}

/** One order for the administrator, with what can be done to it now (decision 0037). */
export interface AdminOrder {
  order: Order;
  /** The production steps that can be taken now: none, or the one that follows the status. */
  nextStatuses: OrderStatus[];
  /** True until the order is shipped or already cancelled. */
  canCancel: boolean;
}

/** The order after a refusal. `refundByHand` is true when the buyer had paid: the money goes back outside the shop. */
export interface CancelledOrder {
  order: AdminOrder;
  refundByHand: boolean;
}

const readStatus: Reader<OrderStatus> = readOneOf(ORDER_STATUSES);

const readListItem: Reader<AdminOrderListItem> = (value, path) => {
  const object = readObject(value, path);
  return {
    number: field(object, 'number', readInteger, path),
    status: field(object, 'status', readStatus, path),
    currency: field(object, 'currency', readString, path),
    total: field(object, 'total', readInteger, path),
    customerName: field(object, 'customerName', readString, path),
    email: field(object, 'email', readString, path),
    itemCount: field(object, 'itemCount', readInteger, path),
    createdAt: field(object, 'createdAt', readString, path),
  };
};

const readPage: Reader<AdminOrderPage> = (value, path) => {
  const object = readObject(value, path);
  const pageSize = field(object, 'pageSize', readInteger, path);
  const totalCount = field(object, 'totalCount', readInteger, path);
  return {
    items: field(object, 'items', readArray(readListItem), path),
    page: field(object, 'page', readInteger, path),
    pageSize,
    totalCount,
    // The server may leave it out; it is then worked out from the other two numbers.
    totalPages:
      object.totalPages === undefined
        ? Math.ceil(totalCount / pageSize)
        : field(object, 'totalPages', readInteger, path),
  };
};

const readAdminOrder: Reader<AdminOrder> = (value, path) => {
  const object = readObject(value, path);
  return {
    order: readOrder(object.order),
    nextStatuses: field(object, 'nextStatuses', readArray(readStatus), path),
    canCancel: field(object, 'canCancel', readBoolean, path),
  };
};

const readCancelled: Reader<CancelledOrder> = (value, path) => {
  const object = readObject(value, path);
  return {
    order: field(object, 'order', readAdminOrder, path),
    refundByHand: field(object, 'refundByHand', readBoolean, path),
  };
};

/** How many orders have each status. Every status is there, zero when it has none. */
export type OrderStatusCounts = Record<OrderStatus, number>;

const readCount: Reader<{ status: OrderStatus; count: number }> = (value, path) => {
  const object = readObject(value, path);
  return {
    status: field(object, 'status', readStatus, path),
    count: field(object, 'count', readInteger, path),
  };
};

/** The number of orders of each status, for the filter of the list. */
export async function getOrderStatusCounts(signal?: AbortSignal): Promise<OrderStatusCounts> {
  const rows = readArray(readCount)(
    await apiRequest(`${ORDERS_PATH}/status-counts`, { signal }),
    'orderStatusCounts',
  );
  const counts = Object.fromEntries(
    ORDER_STATUSES.map((status) => [status, 0]),
  ) as OrderStatusCounts;
  for (const row of rows) {
    counts[row.status] = row.count;
  }
  return counts;
}

/** A page of orders of every status, the newest first. */
export async function getAdminOrders(
  query: AdminOrderQuery,
  signal?: AbortSignal,
): Promise<AdminOrderPage> {
  const params = new URLSearchParams();
  for (const status of query.status ?? []) {
    params.append('status', status);
  }
  if (query.search) {
    params.set('search', query.search);
  }
  params.set('page', String(query.page));
  params.set('pageSize', String(query.pageSize));
  return readPage(await apiRequest(`${ORDERS_PATH}?${params.toString()}`, { signal }), 'orders');
}

/** One order with its lines. An unknown number is an ApiError with status 404 and code `order.not_found`. */
export async function getAdminOrder(number: number, signal?: AbortSignal): Promise<AdminOrder> {
  return readAdminOrder(await apiRequest(`${ORDERS_PATH}/${String(number)}`, { signal }), 'order');
}

/**
 * Takes the order one production step forward. A step that is not the next one, or an order that changed at the same
 * moment, is an ApiError with status 409 and code `order.status_transition_invalid`.
 */
export async function changeOrderStatus(number: number, status: OrderStatus): Promise<AdminOrder> {
  return readAdminOrder(
    await apiSend('POST', `${ORDERS_PATH}/${String(number)}/status`, { status }),
    'order',
  );
}

/** Refuses the order and gives its stock back. A shipped, completed or already cancelled order is 409 `order.status_transition_invalid`. */
export async function cancelOrder(number: number): Promise<CancelledOrder> {
  return readCancelled(
    await apiSend('POST', `${ORDERS_PATH}/${String(number)}/cancel`, {}),
    'cancelled',
  );
}
