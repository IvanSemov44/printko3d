import { adminRequest, adminSend, adminUpload } from './adminHttp';
import type { MoveDirection } from './adminCategories';
import {
  field,
  readArray,
  readBoolean,
  readInteger,
  readNullable,
  readObject,
  readOneOf,
  readString,
  readStringMap,
  type Reader,
} from './decode';

export type ProductStatus = 'Draft' | 'Published' | 'Archived';
export type AdminFulfilmentType = 'InStock' | 'MadeToOrder';

const PRODUCTS_PATH = '/api/v1/admin/products';

/** One row of the admin list. Price and stock count only the active variants. */
export interface AdminProductListItem {
  id: string;
  name: string;
  slug: string;
  categoryId: string;
  categoryName: string;
  status: ProductStatus;
  fulfilmentType: AdminFulfilmentType;
  variantCount: number;
  /** In the currency's smallest unit; null when no variant is active. */
  minPrice: number | null;
  /** Null for a made-to-order product. */
  totalStock: number | null;
  version: number;
}

export interface AdminProductPage {
  items: AdminProductListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/** A variant as the admin panel sees it. Prices are in the currency's smallest unit. */
export interface AdminVariant {
  id: string;
  sku: string;
  name: string;
  price: number;
  compareAtPrice: number | null;
  /** Null for a made-to-order product. */
  stockQuantity: number | null;
  weightGrams: number;
  attributes: Record<string, string>;
  isActive: boolean;
  sortOrder: number;
}

/** An image of a product as the admin panel sees it. The address is built from `storageKey` with `imageUrl`. */
export interface AdminImage {
  id: string;
  /** The variant the image shows, or null for the whole product. */
  variantId: string | null;
  storageKey: string;
  altText: string;
  sortOrder: number;
  isPrimary: boolean;
}

export interface AdminProduct {
  id: string;
  categoryId: string;
  name: string;
  slug: string;
  shortDescription: string;
  description: string;
  fulfilmentType: AdminFulfilmentType;
  leadTimeDays: number | null;
  status: ProductStatus;
  seoTitle: string | null;
  seoDescription: string | null;
  /** The row version the form was loaded with. Send it back; a different value means someone else saved first. */
  version: number;
  variants: AdminVariant[];
  /** In the order of the shop. */
  images: AdminImage[];
}

/** What the product form can change. Images and status have their own operations. */
export interface ProductInput {
  categoryId: string;
  name: string;
  slug: string;
  shortDescription: string;
  description: string;
  fulfilmentType: AdminFulfilmentType;
  /** At least 1 for made-to-order; null otherwise. */
  leadTimeDays: number | null;
  seoTitle: string | null;
  seoDescription: string | null;
  /** Needed only when a made-to-order product becomes in-stock: the stock of every variant, by variant id. */
  variantStock?: Record<string, number>;
}

/** What the variant form can change. Whether it is active and where it stands have their own operations. */
export interface VariantInput {
  sku: string;
  name: string;
  /** In the currency's smallest unit. */
  price: number;
  compareAtPrice: number | null;
  /** Needed for an in-stock product, and must be null for a made-to-order one. */
  stockQuantity: number | null;
  weightGrams: number;
  attributes: Record<string, string>;
}

export interface AdminProductQuery {
  status?: ProductStatus;
  categoryId?: string;
  search?: string;
  page: number;
  pageSize: number;
}

const readStatus: Reader<ProductStatus> = readOneOf(['Draft', 'Published', 'Archived'] as const);
const readFulfilmentType: Reader<AdminFulfilmentType> = readOneOf([
  'InStock',
  'MadeToOrder',
] as const);

const readListItem: Reader<AdminProductListItem> = (value, path) => {
  const object = readObject(value, path);
  return {
    id: field(object, 'id', readString, path),
    name: field(object, 'name', readString, path),
    slug: field(object, 'slug', readString, path),
    categoryId: field(object, 'categoryId', readString, path),
    categoryName: field(object, 'categoryName', readString, path),
    status: field(object, 'status', readStatus, path),
    fulfilmentType: field(object, 'fulfilmentType', readFulfilmentType, path),
    variantCount: field(object, 'variantCount', readInteger, path),
    minPrice: field(object, 'minPrice', readNullable(readInteger), path),
    totalStock: field(object, 'totalStock', readNullable(readInteger), path),
    version: field(object, 'version', readInteger, path),
  };
};

const readPage: Reader<AdminProductPage> = (value, path) => {
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

const readVariant: Reader<AdminVariant> = (value, path) => {
  const object = readObject(value, path);
  return {
    id: field(object, 'id', readString, path),
    sku: field(object, 'sku', readString, path),
    name: field(object, 'name', readString, path),
    price: field(object, 'price', readInteger, path),
    compareAtPrice: field(object, 'compareAtPrice', readNullable(readInteger), path),
    stockQuantity: field(object, 'stockQuantity', readNullable(readInteger), path),
    weightGrams: field(object, 'weightGrams', readInteger, path),
    attributes: field(object, 'attributes', readStringMap, path),
    isActive: field(object, 'isActive', readBoolean, path),
    sortOrder: field(object, 'sortOrder', readInteger, path),
  };
};

const readImage: Reader<AdminImage> = (value, path) => {
  const object = readObject(value, path);
  return {
    id: field(object, 'id', readString, path),
    variantId: field(object, 'variantId', readNullable(readString), path),
    storageKey: field(object, 'storageKey', readString, path),
    altText: field(object, 'altText', readString, path),
    sortOrder: field(object, 'sortOrder', readInteger, path),
    isPrimary: field(object, 'isPrimary', readBoolean, path),
  };
};

const readProduct: Reader<AdminProduct> = (value, path) => {
  const object = readObject(value, path);
  return {
    id: field(object, 'id', readString, path),
    categoryId: field(object, 'categoryId', readString, path),
    name: field(object, 'name', readString, path),
    slug: field(object, 'slug', readString, path),
    shortDescription: field(object, 'shortDescription', readString, path),
    description: field(object, 'description', readString, path),
    fulfilmentType: field(object, 'fulfilmentType', readFulfilmentType, path),
    leadTimeDays: field(object, 'leadTimeDays', readNullable(readInteger), path),
    status: field(object, 'status', readStatus, path),
    seoTitle: field(object, 'seoTitle', readNullable(readString), path),
    seoDescription: field(object, 'seoDescription', readNullable(readString), path),
    version: field(object, 'version', readInteger, path),
    variants: field(object, 'variants', readArray(readVariant), path),
    images: field(object, 'images', readArray(readImage), path),
  };
};

/** A page of products of every status, ordered by name. */
export async function getAdminProducts(
  query: AdminProductQuery,
  signal?: AbortSignal,
): Promise<AdminProductPage> {
  const params = new URLSearchParams();
  if (query.status) {
    params.set('status', query.status);
  }
  if (query.categoryId) {
    params.set('categoryId', query.categoryId);
  }
  if (query.search) {
    params.set('search', query.search);
  }
  params.set('page', String(query.page));
  params.set('pageSize', String(query.pageSize));
  return readPage(
    await adminRequest(`${PRODUCTS_PATH}?${params.toString()}`, { signal }),
    'products',
  );
}

/** One product with all its variants, the inactive ones too. An unknown id is an ApiError with status 404. */
export async function getAdminProduct(id: string, signal?: AbortSignal): Promise<AdminProduct> {
  return readProduct(await adminRequest(`${PRODUCTS_PATH}/${id}`, { signal }), 'product');
}

/** A new product is a draft without variants. A taken slug is an ApiError with status 409 and code `product.slug_taken`. */
export async function createProduct(input: ProductInput): Promise<AdminProduct> {
  return readProduct(await adminSend('POST', PRODUCTS_PATH, input), 'product');
}

/** Also 409 with code `product.version_conflict` when someone saved the product after `version` was read. */
export async function updateProduct(
  id: string,
  input: ProductInput,
  version: number,
): Promise<AdminProduct> {
  return readProduct(
    await adminSend('PUT', `${PRODUCTS_PATH}/${id}`, { ...input, version }),
    'product',
  );
}

/** Publishing needs an active variant (409 `product.cannot_publish_without_active_variant`). */
export async function setProductStatus(
  id: string,
  status: ProductStatus,
  version: number,
): Promise<AdminProduct> {
  return readProduct(
    await adminSend('PUT', `${PRODUCTS_PATH}/${id}/status`, { status, version }),
    'product',
  );
}

/** Adds a variant and returns the whole product. A taken SKU is 409 `variant.sku_taken`. */
export async function addVariant(
  productId: string,
  input: VariantInput,
  sortOrder: number,
): Promise<AdminProduct> {
  return readProduct(
    await adminSend('POST', `${PRODUCTS_PATH}/${productId}/variants`, {
      ...input,
      isActive: true,
      sortOrder,
    }),
    'product',
  );
}

export async function updateVariant(
  productId: string,
  variantId: string,
  input: VariantInput,
): Promise<AdminProduct> {
  return readProduct(
    await adminSend('PUT', `${PRODUCTS_PATH}/${productId}/variants/${variantId}`, input),
    'product',
  );
}

/** The last active variant of a published product cannot be deactivated (409 `variant.last_active`). */
export async function setVariantActive(
  productId: string,
  variantId: string,
  isActive: boolean,
): Promise<AdminProduct> {
  return readProduct(
    await adminSend('PUT', `${PRODUCTS_PATH}/${productId}/variants/${variantId}/active`, {
      isActive,
    }),
    'product',
  );
}

export async function moveVariant(
  productId: string,
  variantId: string,
  direction: MoveDirection,
): Promise<AdminProduct> {
  return readProduct(
    await adminSend('POST', `${PRODUCTS_PATH}/${productId}/variants/${variantId}/move`, {
      direction,
    }),
    'product',
  );
}

/** The largest image the server accepts, and the types it accepts (it decides the type from the content of the file). */
export const IMAGE_MAX_BYTES = 5 * 1024 * 1024;
export const IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const;
/** The most images one product can have. */
export const IMAGE_MAX_COUNT = 20;
/** The longest alt text. */
export const IMAGE_ALT_MAX_LENGTH = 300;

export interface ImageUploadInput {
  file: File;
  altText: string;
  /** A variant of the same product, or null for the whole product. */
  variantId: string | null;
}

/**
 * Uploads one image and returns the whole product. Refusals are ApiErrors: 400 `image.type_not_allowed` or
 * `image.empty`, 413 `image.too_large`, 409 `image.limit_reached`.
 */
export async function uploadProductImage(
  productId: string,
  input: ImageUploadInput,
): Promise<AdminProduct> {
  const form = new FormData();
  form.append('file', input.file);
  form.append('altText', input.altText);
  if (input.variantId !== null) {
    form.append('variantId', input.variantId);
  }
  return readProduct(await adminUpload(`${PRODUCTS_PATH}/${productId}/images`, form), 'product');
}

/** Replaces the alt text and the variant of an image. */
export async function updateProductImage(
  productId: string,
  imageId: string,
  input: { altText: string; variantId: string | null },
): Promise<AdminProduct> {
  return readProduct(
    await adminSend('PUT', `${PRODUCTS_PATH}/${productId}/images/${imageId}`, input),
    'product',
  );
}

/** The old primary image becomes an ordinary one. */
export async function setPrimaryProductImage(
  productId: string,
  imageId: string,
): Promise<AdminProduct> {
  return readProduct(
    await adminSend('PUT', `${PRODUCTS_PATH}/${productId}/images/${imageId}/primary`, {}),
    'product',
  );
}

export async function moveProductImage(
  productId: string,
  imageId: string,
  direction: MoveDirection,
): Promise<AdminProduct> {
  return readProduct(
    await adminSend('POST', `${PRODUCTS_PATH}/${productId}/images/${imageId}/move`, {
      direction,
    }),
    'product',
  );
}

/** Deletes the row and the file. If it was the primary image, the first one in order becomes primary. */
export async function deleteProductImage(
  productId: string,
  imageId: string,
): Promise<AdminProduct> {
  return readProduct(
    await adminSend('DELETE', `${PRODUCTS_PATH}/${productId}/images/${imageId}`, undefined),
    'product',
  );
}
