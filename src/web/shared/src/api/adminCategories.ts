import { adminRequest, adminSend } from './adminHttp';
import {
  field,
  readArray,
  readBoolean,
  readInteger,
  readNullable,
  readObject,
  readString,
  type Reader,
} from './decode';
import type { components } from './schema';

type CategoryAdminDto = components['schemas']['CategoryAdminDto'];

/** A category as the admin panel sees it. The integer fields are real numbers (the generated types allow strings). */
export type AdminCategory = Omit<CategoryAdminDto, 'sortOrder' | 'productCount' | 'version'> & {
  sortOrder: number;
  productCount: number;
  /** The row version the form was loaded with. Send it back; a different value means someone else saved first. */
  version: number;
};

/** What the category form can change. The image is set by a later task. */
export interface CategoryInput {
  name: string;
  slug: string;
  parentId: string | null;
  description: string | null;
  sortOrder: number;
  isActive: boolean;
}

export type MoveDirection = components['schemas']['MoveDirection'];

// Relative on purpose, like the sign-in: the panel reaches the API through its own address (decision 0014).
const CATEGORIES_PATH = '/api/v1/admin/categories';

const readAdminCategory: Reader<AdminCategory> = (value, path) => {
  const object = readObject(value, path);
  return {
    id: field(object, 'id', readString, path),
    parentId: field(object, 'parentId', readNullable(readString), path),
    name: field(object, 'name', readString, path),
    slug: field(object, 'slug', readString, path),
    description: field(object, 'description', readNullable(readString), path),
    sortOrder: field(object, 'sortOrder', readInteger, path),
    isActive: field(object, 'isActive', readBoolean, path),
    productCount: field(object, 'productCount', readInteger, path),
    version: field(object, 'version', readInteger, path),
  };
};

const readAdminCategories = readArray(readAdminCategory);

/** All categories, the inactive ones too, without paging (decision 0016). */
export async function getAdminCategories(signal?: AbortSignal): Promise<AdminCategory[]> {
  return readAdminCategories(await adminRequest(CATEGORIES_PATH, { signal }), 'categories');
}

/** A taken slug is an ApiError with status 409 and code `category.slug_taken`. */
export async function createCategory(input: CategoryInput): Promise<AdminCategory> {
  return readAdminCategory(await adminSend('POST', CATEGORIES_PATH, input), 'category');
}

/** Also 409 with code `category.version_conflict` when someone saved the category after `version` was read. */
export async function updateCategory(
  id: string,
  input: CategoryInput,
  version: number,
): Promise<AdminCategory> {
  return readAdminCategory(
    await adminSend('PUT', `${CATEGORIES_PATH}/${id}`, { ...input, version }),
    'category',
  );
}

export async function setCategoryActive(
  id: string,
  isActive: boolean,
  version: number,
): Promise<AdminCategory> {
  const action = isActive ? 'activate' : 'deactivate';
  return readAdminCategory(
    await adminSend('POST', `${CATEGORIES_PATH}/${id}/${action}`, { version }),
    'category',
  );
}

/** Moves the category one place among its siblings and returns the whole list with fresh versions. */
export async function moveCategory(
  id: string,
  direction: MoveDirection,
  version: number,
): Promise<AdminCategory[]> {
  return readAdminCategories(
    await adminSend('POST', `${CATEGORIES_PATH}/${id}/move`, { direction, version }),
    'categories',
  );
}
