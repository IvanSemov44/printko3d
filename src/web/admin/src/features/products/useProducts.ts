import {
  addVariant,
  createProduct,
  deleteProductImage,
  getAdminProduct,
  getAdminProducts,
  getStoreMoneySettings,
  loadRuntimeConfig,
  moveProductImage,
  moveVariant,
  setPrimaryProductImage,
  setProductStatus,
  setVariantActive,
  updateProduct,
  updateProductImage,
  updateVariant,
  uploadProductImage,
  type AdminProduct,
  type ImageUploadInput,
  type AdminProductQuery,
  type MoveDirection,
  type ProductInput,
  type ProductStatus,
  type VariantInput,
} from '@storefront/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

// The first part is "admin", so signing out removes everything loaded for the administrator.
const PRODUCTS_KEY = ['admin', 'products'] as const;

/** The currency and locale of the shop. The settings screen replaces it after a save. */
export const STORE_MONEY_KEY = ['admin', 'store-money'] as const;
const LIST_KEY = [...PRODUCTS_KEY, 'list'] as const;
const itemKey = (id: string) => [...PRODUCTS_KEY, 'item', id] as const;

/** A page of the admin list. Always the API's answer: the versions in it must be current. */
export function useAdminProducts(query: AdminProductQuery) {
  return useQuery({
    queryKey: [...LIST_KEY, query],
    queryFn: ({ signal }) => getAdminProducts(query, signal),
    // The list stays on screen while the next page loads.
    placeholderData: (previous) => previous,
  });
}

/** One product with its variants. No refetch in the background: the form keeps the version it was loaded with. */
export function useAdminProduct(id: string | undefined) {
  return useQuery({
    queryKey: itemKey(id ?? ''),
    queryFn: ({ signal }) => getAdminProduct(id ?? '', signal),
    enabled: id !== undefined,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
}

/** The currency and locale of the shop, for typing and showing prices. They hardly ever change. */
export function useStoreMoney() {
  return useQuery({
    queryKey: STORE_MONEY_KEY,
    queryFn: ({ signal }) => getStoreMoneySettings(signal),
    staleTime: Infinity,
  });
}

/** After a change the product in the answer replaces the one in the cache, and the lists are loaded again. */
function useRememberProduct() {
  const queryClient = useQueryClient();
  return (product: AdminProduct) => {
    queryClient.setQueryData(itemKey(product.id), product);
    return queryClient.invalidateQueries({ queryKey: LIST_KEY });
  };
}

/** After a failure the copy on screen may be out of date (a conflict), so it is loaded again. */
function useForgetProduct() {
  const queryClient = useQueryClient();
  return (id: string) => queryClient.invalidateQueries({ queryKey: itemKey(id) });
}

export function useCreateProduct() {
  const remember = useRememberProduct();
  return useMutation({
    mutationFn: (input: ProductInput) => createProduct(input),
    onSuccess: remember,
  });
}

export function useUpdateProduct() {
  const remember = useRememberProduct();
  return useMutation({
    mutationFn: (change: { id: string; input: ProductInput; version: number }) =>
      updateProduct(change.id, change.input, change.version),
    onSuccess: remember,
  });
}

export function useSetProductStatus() {
  const remember = useRememberProduct();
  const forget = useForgetProduct();
  return useMutation({
    mutationFn: (change: { id: string; status: ProductStatus; version: number }) =>
      setProductStatus(change.id, change.status, change.version),
    onSuccess: remember,
    onError: (_error, change) => forget(change.id),
  });
}

export function useAddVariant(productId: string) {
  const remember = useRememberProduct();
  return useMutation({
    mutationFn: (change: { input: VariantInput; sortOrder: number }) =>
      addVariant(productId, change.input, change.sortOrder),
    onSuccess: remember,
  });
}

export function useUpdateVariant(productId: string) {
  const remember = useRememberProduct();
  return useMutation({
    mutationFn: (change: { variantId: string; input: VariantInput }) =>
      updateVariant(productId, change.variantId, change.input),
    onSuccess: remember,
  });
}

export function useSetVariantActive(productId: string) {
  const remember = useRememberProduct();
  const forget = useForgetProduct();
  return useMutation({
    mutationFn: (change: { variantId: string; isActive: boolean }) =>
      setVariantActive(productId, change.variantId, change.isActive),
    onSuccess: remember,
    // A refusal means the copy on screen is out of date (for example the last active variant).
    onError: () => forget(productId),
  });
}

export function useMoveVariant(productId: string) {
  const remember = useRememberProduct();
  const forget = useForgetProduct();
  return useMutation({
    mutationFn: (change: { variantId: string; direction: MoveDirection }) =>
      moveVariant(productId, change.variantId, change.direction),
    onSuccess: remember,
    onError: () => forget(productId),
  });
}

/**
 * Where the images are served from (decision 0011), read from /config.json. `null` while it loads, when the file cannot
 * be read, and when the environment has no image storage: the panel then shows a placeholder instead of a preview.
 */
export function useImageBaseUrl(): string | null {
  const config = useQuery({
    queryKey: ['admin', 'runtime-config'] as const,
    queryFn: ({ signal }) => loadRuntimeConfig(signal),
    staleTime: Infinity,
    retry: false,
    refetchOnWindowFocus: false,
  });
  return config.data?.imageBaseUrl ?? null;
}

export function useUploadImage(productId: string) {
  const remember = useRememberProduct();
  return useMutation({
    mutationFn: (input: ImageUploadInput) => uploadProductImage(productId, input),
    onSuccess: remember,
  });
}

export function useUpdateImage(productId: string) {
  const remember = useRememberProduct();
  return useMutation({
    mutationFn: (change: { imageId: string; altText: string; variantId: string | null }) =>
      updateProductImage(productId, change.imageId, {
        altText: change.altText,
        variantId: change.variantId,
      }),
    onSuccess: remember,
  });
}

export function useSetPrimaryImage(productId: string) {
  const remember = useRememberProduct();
  const forget = useForgetProduct();
  return useMutation({
    mutationFn: (imageId: string) => setPrimaryProductImage(productId, imageId),
    onSuccess: remember,
    // A refusal means the copy on screen is out of date (another image became primary a moment earlier).
    onError: () => forget(productId),
  });
}

export function useMoveImage(productId: string) {
  const remember = useRememberProduct();
  const forget = useForgetProduct();
  return useMutation({
    mutationFn: (change: { imageId: string; direction: MoveDirection }) =>
      moveProductImage(productId, change.imageId, change.direction),
    onSuccess: remember,
    onError: () => forget(productId),
  });
}

export function useDeleteImage(productId: string) {
  const remember = useRememberProduct();
  const forget = useForgetProduct();
  return useMutation({
    mutationFn: (imageId: string) => deleteProductImage(productId, imageId),
    onSuccess: remember,
    onError: () => forget(productId),
  });
}
