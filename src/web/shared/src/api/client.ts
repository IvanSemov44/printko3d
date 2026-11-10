import { loadRuntimeConfig, type RuntimeConfig } from '../config';
import { imageUrl } from '../images';
import { toSafeInteger } from '../numbers';
import type { components } from './schema';

export type StoreSettings = components['schemas']['StoreSettingsDto'];
export type Category = components['schemas']['CategoryDto'];
export type FulfilmentType = components['schemas']['FulfilmentType'];

type ProductListItemDto = components['schemas']['ProductListItemDto'];
type ProductPageDto = components['schemas']['PagedResultOfProductListItemDto'];

/** The main image of a card. `url` is null when the environment has no image storage (decision 0011). */
export interface ProductListImage {
  url: string | null;
  altText: string;
}

/** A product card; the integer fields are real numbers (the generated types allow strings). */
export type ProductListItem = Omit<
  ProductListItemDto,
  'minPrice' | 'leadTimeDays' | 'primaryImage'
> & {
  /** In the currency's smallest unit. */
  minPrice: number;
  leadTimeDays: number | null;
  /** Null when the product has no image: the card then shows the placeholder. */
  primaryImage: ProductListImage | null;
};

type ProductDetailsDto = components['schemas']['ProductDetailsDto'];
type ProductVariantDto = components['schemas']['ProductVariantDto'];
type ProductImageDto = components['schemas']['ProductImageDto'];

/** A purchasable variant; prices are in the currency's smallest unit. */
export type ProductVariant = Omit<
  ProductVariantDto,
  'price' | 'compareAtPrice' | 'weightGrams' | 'sortOrder'
> & {
  price: number;
  compareAtPrice: number | null;
  weightGrams: number;
  sortOrder: number;
};

/** A product image. `url` is null when the environment has no image storage (decision 0011). */
export type ProductImage = Omit<ProductImageDto, 'sortOrder'> & {
  sortOrder: number;
  url: string | null;
};

/** A product page; variants and images are ordered by `sortOrder`. */
export type ProductDetails = Omit<ProductDetailsDto, 'leadTimeDays' | 'variants' | 'images'> & {
  leadTimeDays: number | null;
  variants: ProductVariant[];
  images: ProductImage[];
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
  /** For a 400 answer: the names of the fields that were rejected, as the API wrote them (camelCase). */
  readonly fields: readonly string[];

  constructor(status: number, code: string | null, fields: readonly string[] = []) {
    super(`API request failed with status ${String(status)}`);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.fields = fields;
  }
}

export async function readErrorCode(response: Response): Promise<string | null> {
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

async function getJsonWithConfig(
  path: string,
  signal?: AbortSignal,
): Promise<{ body: unknown; config: RuntimeConfig }> {
  const config = await loadRuntimeConfig(signal);
  const response = await fetch(`${config.apiUrl}${path}`, { signal });
  if (!response.ok) {
    throw new ApiError(response.status, await readErrorCode(response));
  }
  return { body: await response.json(), config };
}

async function getJson(path: string, signal?: AbortSignal): Promise<unknown> {
  return (await getJsonWithConfig(path, signal)).body;
}

export async function getStoreSettings(signal?: AbortSignal): Promise<StoreSettings> {
  return (await getJson('/api/v1/store-settings', signal)) as StoreSettings;
}

export async function getCategories(signal?: AbortSignal): Promise<Category[]> {
  return (await getJson('/api/v1/categories', signal)) as Category[];
}

function toProductListItem(item: ProductListItemDto, imageBaseUrl: string | null): ProductListItem {
  return {
    ...item,
    minPrice: toSafeInteger(item.minPrice),
    leadTimeDays: item.leadTimeDays === null ? null : toSafeInteger(item.leadTimeDays),
    primaryImage: item.primaryImage
      ? {
          url: imageUrl(imageBaseUrl, item.primaryImage.storageKey),
          altText: item.primaryImage.altText,
        }
      : null,
  };
}

export async function getProducts(query: ProductQuery, signal?: AbortSignal): Promise<ProductPage> {
  const params = new URLSearchParams({
    category: query.category,
    page: String(query.page),
    pageSize: String(query.pageSize),
  });
  const { body, config } = await getJsonWithConfig(`/api/v1/products?${params.toString()}`, signal);
  const dto = body as ProductPageDto;
  const pageSize = toSafeInteger(dto.pageSize);
  const totalCount = toSafeInteger(dto.totalCount);
  return {
    items: dto.items.map((item) => toProductListItem(item, config.imageBaseUrl)),
    page: toSafeInteger(dto.page),
    pageSize,
    totalCount,
    totalPages:
      dto.totalPages === undefined
        ? Math.ceil(totalCount / pageSize)
        : toSafeInteger(dto.totalPages),
  };
}

function bySortOrder<T extends { sortOrder: number }>(items: T[]): T[] {
  return [...items].sort((a, b) => a.sortOrder - b.sortOrder);
}

function toProductVariant(variant: ProductVariantDto): ProductVariant {
  return {
    ...variant,
    price: toSafeInteger(variant.price),
    compareAtPrice: variant.compareAtPrice === null ? null : toSafeInteger(variant.compareAtPrice),
    weightGrams: toSafeInteger(variant.weightGrams),
    sortOrder: toSafeInteger(variant.sortOrder),
  };
}

function toProductImage(image: ProductImageDto, imageBaseUrl: string | null): ProductImage {
  return {
    ...image,
    sortOrder: toSafeInteger(image.sortOrder),
    url: imageUrl(imageBaseUrl, image.storageKey),
  };
}

/** One published product. An unknown, unpublished or hidden product is an ApiError with status 404. */
export async function getProduct(slug: string, signal?: AbortSignal): Promise<ProductDetails> {
  const { body, config } = await getJsonWithConfig(
    `/api/v1/products/${encodeURIComponent(slug)}`,
    signal,
  );
  const dto = body as ProductDetailsDto;
  return {
    ...dto,
    leadTimeDays: dto.leadTimeDays === null ? null : toSafeInteger(dto.leadTimeDays),
    variants: bySortOrder(dto.variants.map(toProductVariant)),
    images: bySortOrder(dto.images.map((image) => toProductImage(image, config.imageBaseUrl))),
  };
}

/** The names of the fields in the `errors` of a 400 ProblemDetails answer, or an empty list for any other body. */
export async function readErrorFields(response: Response): Promise<string[]> {
  try {
    const body: unknown = await response.clone().json();
    if (typeof body === 'object' && body !== null && 'errors' in body) {
      const errors = body.errors;
      if (typeof errors === 'object' && errors !== null) {
        return Object.keys(errors);
      }
    }
  } catch {
    // The body is not JSON; there are no fields to name.
  }
  return [];
}
