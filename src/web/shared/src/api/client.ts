import { loadRuntimeConfig } from '../config';
import { toSafeInteger } from '../numbers';
import type { components } from './schema';

export type StoreSettings = components['schemas']['StoreSettingsDto'];
export type Category = components['schemas']['CategoryDto'];
export type FulfilmentType = components['schemas']['FulfilmentType'];

type ProductListItemDto = components['schemas']['ProductListItemDto'];
type ProductPageDto = components['schemas']['PagedResultOfProductListItemDto'];

/** A product card; the integer fields are real numbers (the generated types allow strings). */
export type ProductListItem = Omit<ProductListItemDto, 'minPrice' | 'leadTimeDays'> & {
  /** In the currency's smallest unit. */
  minPrice: number;
  leadTimeDays: number | null;
};

export interface ProductPage {
  items: ProductListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ProductQuery {
  category: string;
  page: number;
  pageSize: number;
}

/** A non-2xx API answer. `code` is the stable error code from ProblemDetails, when present. */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string | null;

  constructor(status: number, code: string | null) {
    super(`API request failed with status ${String(status)}`);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

async function readErrorCode(response: Response): Promise<string | null> {
  try {
    const body: unknown = await response.json();
    if (typeof body === 'object' && body !== null && 'code' in body) {
      return typeof body.code === 'string' ? body.code : null;
    }
  } catch {
    // The body is not JSON; the status alone describes the failure.
  }
  return null;
}

async function getJson(path: string, signal?: AbortSignal): Promise<unknown> {
  const { apiUrl } = await loadRuntimeConfig(signal);
  const response = await fetch(`${apiUrl}${path}`, { signal });
  if (!response.ok) {
    throw new ApiError(response.status, await readErrorCode(response));
  }
  return response.json();
}

export async function getStoreSettings(signal?: AbortSignal): Promise<StoreSettings> {
  return (await getJson('/api/v1/store-settings', signal)) as StoreSettings;
}

export async function getCategories(signal?: AbortSignal): Promise<Category[]> {
  return (await getJson('/api/v1/categories', signal)) as Category[];
}

function toProductListItem(item: ProductListItemDto): ProductListItem {
  return {
    ...item,
    minPrice: toSafeInteger(item.minPrice),
    leadTimeDays: item.leadTimeDays === null ? null : toSafeInteger(item.leadTimeDays),
  };
}

export async function getProducts(query: ProductQuery, signal?: AbortSignal): Promise<ProductPage> {
  const params = new URLSearchParams({
    category: query.category,
    page: String(query.page),
    pageSize: String(query.pageSize),
  });
  const dto = (await getJson(`/api/v1/products?${params.toString()}`, signal)) as ProductPageDto;
  const pageSize = toSafeInteger(dto.pageSize);
  const totalCount = toSafeInteger(dto.totalCount);
  return {
    items: dto.items.map(toProductListItem),
    page: toSafeInteger(dto.page),
    pageSize,
    totalCount,
    totalPages:
      dto.totalPages === undefined
        ? Math.ceil(totalCount / pageSize)
        : toSafeInteger(dto.totalPages),
  };
}
