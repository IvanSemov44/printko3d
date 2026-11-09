import { toSafeInteger } from '../numbers';
import { ApiError, readErrorCode } from './client';
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

/** Every request that changes data must carry this header, or the API answers 403 (decision 0014). */
const CSRF_HEADERS = { 'X-Requested-With': 'XMLHttpRequest' };

function toAdminCategory(dto: CategoryAdminDto): AdminCategory {
  return {
    ...dto,
    sortOrder: toSafeInteger(dto.sortOrder),
    productCount: toSafeInteger(dto.productCount),
    version: toSafeInteger(dto.version),
  };
}

async function request(url: string, init: RequestInit): Promise<unknown> {
  const response = await fetch(url, init);
  if (!response.ok) {
    throw new ApiError(response.status, await readErrorCode(response));
  }
  return response.json();
}

function send(method: string, url: string, body: unknown): Promise<unknown> {
  return request(url, {
    method,
    headers: { ...CSRF_HEADERS, 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

/** All categories, the inactive ones too, without paging (decision 0016). */
export async function getAdminCategories(signal?: AbortSignal): Promise<AdminCategory[]> {
  const body = (await request(CATEGORIES_PATH, { signal })) as CategoryAdminDto[];
  return body.map(toAdminCategory);
}

/** A taken slug is an ApiError with status 409 and code `category.slug_taken`. */
export async function createCategory(input: CategoryInput): Promise<AdminCategory> {
  return toAdminCategory((await send('POST', CATEGORIES_PATH, input)) as CategoryAdminDto);
}

/** Also 409 with code `category.version_conflict` when someone saved the category after `version` was read. */
export async function updateCategory(
  id: string,
  input: CategoryInput,
  version: number,
): Promise<AdminCategory> {
  return toAdminCategory(
    (await send('PUT', `${CATEGORIES_PATH}/${id}`, { ...input, version })) as CategoryAdminDto,
  );
}

export async function setCategoryActive(
  id: string,
  isActive: boolean,
  version: number,
): Promise<AdminCategory> {
  const action = isActive ? 'activate' : 'deactivate';
  return toAdminCategory(
    (await send('POST', `${CATEGORIES_PATH}/${id}/${action}`, { version })) as CategoryAdminDto,
  );
}

/** Moves the category one place among its siblings and returns the whole list with fresh versions. */
export async function moveCategory(
  id: string,
  direction: MoveDirection,
  version: number,
): Promise<AdminCategory[]> {
  const body = (await send('POST', `${CATEGORIES_PATH}/${id}/move`, {
    direction,
    version,
  })) as CategoryAdminDto[];
  return body.map(toAdminCategory);
}
