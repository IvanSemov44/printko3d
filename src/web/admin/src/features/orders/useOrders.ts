import {
  cancelOrder,
  changeOrderStatus,
  getAdminOrder,
  getAdminOrders,
  getOrderStatusCounts,
  type AdminOrder,
  type AdminOrderQuery,
  type OrderStatus,
} from '@storefront/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

// The first part is "admin", so signing out removes the orders (with the contact of the buyers) from the cache.
const ORDERS_KEY = ['admin', 'orders'] as const;
const LIST_KEY = [...ORDERS_KEY, 'list'] as const;
const itemKey = (number: number) => [...ORDERS_KEY, 'item', number] as const;

/** A page of the list. The list stays on screen while the next page loads. */
export function useAdminOrders(query: AdminOrderQuery) {
  return useQuery({
    queryKey: [...LIST_KEY, query],
    queryFn: ({ signal }) => getAdminOrders(query, signal),
    placeholderData: (previous) => previous,
  });
}

/** How many orders each status has, for the filter of the list. Loaded again after every change of an order. */
export function useOrderStatusCounts() {
  return useQuery({
    queryKey: [...LIST_KEY, 'counts'],
    queryFn: ({ signal }) => getOrderStatusCounts(signal),
    placeholderData: (previous) => previous,
  });
}

/** One order. No refetch in the background: the buttons are only as new as the last answer. */
export function useAdminOrder(number: number) {
  return useQuery({
    queryKey: itemKey(number),
    queryFn: ({ signal }) => getAdminOrder(number, signal),
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
}

/** After a change the answer replaces the order in the cache, and the lists are loaded again. */
function useRememberOrder(number: number) {
  const queryClient = useQueryClient();
  return (order: AdminOrder) => {
    queryClient.setQueryData(itemKey(number), order);
    void queryClient.invalidateQueries({ queryKey: LIST_KEY });
  };
}

/** Another change at the same moment (a payment, the expiry) leaves the buttons out of date: load the order again. */
function useReloadOrder(number: number) {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: itemKey(number) });
    void queryClient.invalidateQueries({ queryKey: LIST_KEY });
  };
}

export function useChangeOrderStatus(number: number) {
  const remember = useRememberOrder(number);
  const reload = useReloadOrder(number);
  return useMutation({
    mutationFn: (status: OrderStatus) => changeOrderStatus(number, status),
    onSuccess: remember,
    onError: reload,
  });
}

export function useCancelOrder(number: number) {
  const remember = useRememberOrder(number);
  const reload = useReloadOrder(number);
  return useMutation({
    mutationFn: () => cancelOrder(number),
    onSuccess: (cancelled) => {
      remember(cancelled.order);
    },
    onError: reload,
  });
}
