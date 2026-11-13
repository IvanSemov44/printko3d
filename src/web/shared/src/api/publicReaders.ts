import { imageUrl } from '../images';
import type {
  Category,
  FulfilmentType,
  ProductDetails,
  ProductImage,
  ProductListItem,
  ProductPage,
  ProductVariant,
  StoreSettings,
} from './client';
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

const FULFILMENT_TYPES: readonly FulfilmentType[] = ['InStock', 'MadeToOrder'];

/** Checks the shape of what the public API sent, so a wrong answer is named here and not shown as NaN later. */
export function readStoreSettings(value: unknown): StoreSettings {
  const object = readObject(value, 'storeSettings');
  const path = 'storeSettings';
  return {
    storeName: field(object, 'storeName', readString, path),
    defaultCurrency: field(object, 'defaultCurrency', readString, path),
    defaultLocale: field(object, 'defaultLocale', readString, path),
    contactEmail: field(object, 'contactEmail', readString, path),
    contactPhone: field(object, 'contactPhone', readString, path),
    logoKey: field(object, 'logoKey', readNullable(readString), path),
    primaryColor: field(object, 'primaryColor', readString, path),
    isDemo: field(object, 'isDemo', readBoolean, path),
  };
}

const readCategory: Reader<Category> = (value, path) => {
  const object = readObject(value, path);
  return {
    id: field(object, 'id', readString, path),
    parentId: field(object, 'parentId', readNullable(readString), path),
    name: field(object, 'name', readString, path),
    slug: field(object, 'slug', readString, path),
    description: field(object, 'description', readNullable(readString), path),
    imageKey: field(object, 'imageKey', readNullable(readString), path),
    sortOrder: field(object, 'sortOrder', readInteger, path),
  };
};

export function readCategories(value: unknown): Category[] {
  return readArray(readCategory)(value, 'categories');
}

function readCard(imageBaseUrl: string | null): Reader<ProductListItem> {
  return (value, path) => {
    const object = readObject(value, path);
    const readImage: Reader<NonNullable<ProductListItem['primaryImage']>> = (image, imagePath) => {
      const ref = readObject(image, imagePath);
      return {
        url: imageUrl(imageBaseUrl, field(ref, 'storageKey', readString, imagePath)),
        altText: field(ref, 'altText', readString, imagePath),
      };
    };
    return {
      id: field(object, 'id', readString, path),
      name: field(object, 'name', readString, path),
      slug: field(object, 'slug', readString, path),
      shortDescription: field(object, 'shortDescription', readString, path),
      fulfilmentType: field(object, 'fulfilmentType', readOneOf(FULFILMENT_TYPES), path),
      leadTimeDays: field(object, 'leadTimeDays', readNullable(readInteger), path),
      minPrice: field(object, 'minPrice', readInteger, path),
      primaryImage: field(object, 'primaryImage', readNullable(readImage), path),
      isAvailable: field(object, 'isAvailable', readBoolean, path),
    };
  };
}

export function readProductPage(value: unknown, imageBaseUrl: string | null): ProductPage {
  const path = 'productPage';
  const object = readObject(value, path);
  const pageSize = field(object, 'pageSize', readInteger, path);
  const totalCount = field(object, 'totalCount', readInteger, path);
  return {
    items: field(object, 'items', readArray(readCard(imageBaseUrl)), path),
    page: field(object, 'page', readInteger, path),
    pageSize,
    totalCount,
    // The API may leave the page count out; it is then worked out from the other two numbers.
    totalPages:
      object['totalPages'] === undefined
        ? Math.ceil(totalCount / pageSize)
        : field(object, 'totalPages', readInteger, path),
  };
}

const readVariant: Reader<ProductVariant> = (value, path) => {
  const object = readObject(value, path);
  return {
    id: field(object, 'id', readString, path),
    sku: field(object, 'sku', readString, path),
    name: field(object, 'name', readString, path),
    price: field(object, 'price', readInteger, path),
    compareAtPrice: field(object, 'compareAtPrice', readNullable(readInteger), path),
    isAvailable: field(object, 'isAvailable', readBoolean, path),
    weightGrams: field(object, 'weightGrams', readInteger, path),
    attributes: field(object, 'attributes', readStringMap, path),
    sortOrder: field(object, 'sortOrder', readInteger, path),
  };
};

function readImageOf(imageBaseUrl: string | null): Reader<ProductImage> {
  return (value, path) => {
    const object = readObject(value, path);
    const storageKey = field(object, 'storageKey', readString, path);
    return {
      id: field(object, 'id', readString, path),
      variantId: field(object, 'variantId', readNullable(readString), path),
      storageKey,
      altText: field(object, 'altText', readString, path),
      sortOrder: field(object, 'sortOrder', readInteger, path),
      isPrimary: field(object, 'isPrimary', readBoolean, path),
      url: imageUrl(imageBaseUrl, storageKey),
    };
  };
}

function bySortOrder<T extends { sortOrder: number }>(items: T[]): T[] {
  return [...items].sort((a, b) => a.sortOrder - b.sortOrder);
}

/** One product; variants and images are ordered by `sortOrder`. */
export function readProductDetails(value: unknown, imageBaseUrl: string | null): ProductDetails {
  const path = 'product';
  const object = readObject(value, path);
  return {
    id: field(object, 'id', readString, path),
    name: field(object, 'name', readString, path),
    slug: field(object, 'slug', readString, path),
    shortDescription: field(object, 'shortDescription', readString, path),
    description: field(object, 'description', readString, path),
    fulfilmentType: field(object, 'fulfilmentType', readOneOf(FULFILMENT_TYPES), path),
    leadTimeDays: field(object, 'leadTimeDays', readNullable(readInteger), path),
    seoTitle: field(object, 'seoTitle', readString, path),
    seoDescription: field(object, 'seoDescription', readString, path),
    isAvailable: field(object, 'isAvailable', readBoolean, path),
    variants: bySortOrder(field(object, 'variants', readArray(readVariant), path)),
    images: bySortOrder(field(object, 'images', readArray(readImageOf(imageBaseUrl)), path)),
  };
}
