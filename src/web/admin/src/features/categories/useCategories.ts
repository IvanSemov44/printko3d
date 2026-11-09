import {
  createCategory,
  getAdminCategories,
  moveCategory,
  setCategoryActive,
  updateCategory,
  type AdminCategory,
  type CategoryInput,
  type MoveDirection,
} from '@storefront/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

// The first part is "admin", so signing out removes this list with everything else loaded for the administrator.
const CATEGORIES_KEY = ['admin', 'categories'] as const;

/** Every category, the inactive ones too. Always the API's answer: the versions in it must be current. */
export function useAdminCategories(options: { refetchInBackground?: boolean } = {}) {
  const refetchInBackground = options.refetchInBackground ?? true;
  return useQuery({
    queryKey: CATEGORIES_KEY,
    queryFn: ({ signal }) => getAdminCategories(signal),
    refetchOnWindowFocus: refetchInBackground,
    refetchOnReconnect: refetchInBackground,
  });
}

export function useCreateCategory() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CategoryInput) => createCategory(input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: CATEGORIES_KEY }),
  });
}

export function useUpdateCategory() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (change: { id: string; input: CategoryInput; version: number }) =>
      updateCategory(change.id, change.input, change.version),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: CATEGORIES_KEY }),
  });
}

export function useSetCategoryActive() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (change: { id: string; isActive: boolean; version: number }) =>
      setCategoryActive(change.id, change.isActive, change.version),
    // After a failure too: a conflict means the list on screen is out of date.
    onSettled: () => queryClient.invalidateQueries({ queryKey: CATEGORIES_KEY }),
  });
}

export function useMoveCategory() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (change: { id: string; direction: MoveDirection; version: number }) =>
      moveCategory(change.id, change.direction, change.version),
    onSuccess: (list: AdminCategory[]) => {
      queryClient.setQueryData(CATEGORIES_KEY, list);
    },
    onError: () => queryClient.invalidateQueries({ queryKey: CATEGORIES_KEY }),
  });
}
