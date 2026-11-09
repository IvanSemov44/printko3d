import { ApiError, type AdminCategory, type CategoryInput } from '@storefront/shared';
import { useState, type SubmitEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { t, type MessageKey } from '../../i18n';
import {
  DESCRIPTION_MAX_LENGTH,
  NAME_MAX_LENGTH,
  validateCategory,
  type CategoryFormErrors,
} from './categoryValidation';
import { SLUG_MAX_LENGTH, slugFromName } from './slug';
import { useAdminCategories, useCreateCategory, useUpdateCategory } from './useCategories';

const FIELD_ORDER = ['name', 'slug', 'parentId', 'description', 'sortOrder'] as const;
type FieldName = (typeof FIELD_ORDER)[number];
type FieldErrors = CategoryFormErrors & { parentId?: MessageKey };

function describedBy(...ids: (string | false)[]): string | undefined {
  const used = ids.filter((id) => id !== false);
  return used.length > 0 ? used.join(' ') : undefined;
}

interface FormProps {
  category: AdminCategory | undefined;
  categories: readonly AdminCategory[];
  onReload: () => void;
}

function CategoryForm({ category, categories, onReload }: FormProps) {
  const navigate = useNavigate();
  const create = useCreateCategory();
  const update = useUpdateCategory();
  const save = category ? update : create;
  const isEdit = category !== undefined;

  // A new category goes to the end of the list unless the administrator says otherwise.
  const nextSortOrder = Math.max(-1, ...categories.map((item) => item.sortOrder)) + 1;
  const [name, setName] = useState(category?.name ?? '');
  const [slug, setSlug] = useState(category?.slug ?? '');
  // The slug follows the name only for a new category, and only until the administrator edits it.
  const [slugEdited, setSlugEdited] = useState(isEdit);
  const [parentId, setParentId] = useState(category?.parentId ?? '');
  const [description, setDescription] = useState(category?.description ?? '');
  const [sortOrder, setSortOrder] = useState(String(category?.sortOrder ?? nextSortOrder));
  const [isActive, setIsActive] = useState(category?.isActive ?? true);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<MessageKey | null>(null);
  const [conflict, setConflict] = useState(false);

  // Only top-level categories can be a parent (the tree has two levels), never the category itself,
  // and a category that has subcategories cannot become a subcategory.
  const hasChildren = category ? categories.some((item) => item.parentId === category.id) : false;
  const parentOptions = categories.filter(
    (item) => item.parentId === null && item.id !== category?.id,
  );

  function focusField(form: HTMLFormElement, field: string) {
    const element = form.elements.namedItem(field);
    if (element instanceof HTMLElement) {
      element.focus();
    }
  }

  function handleError(error: Error, form: HTMLFormElement) {
    const code = error instanceof ApiError ? error.code : null;
    if (error instanceof ApiError && error.status === 409 && code === 'category.slug_taken') {
      setErrors({ slug: 'categories.error.slugTaken' });
      focusField(form, 'slug');
    } else if (error instanceof ApiError && error.status === 409) {
      setConflict(true);
    } else if (error instanceof ApiError && code === 'category.parent_not_found') {
      setErrors({ parentId: 'categories.error.parentNotFound' });
      focusField(form, 'parentId');
    } else if (error instanceof ApiError && error.status === 400) {
      setFormError('categories.error.rejected');
    } else {
      setFormError('categories.error.saveFailed');
    }
  }

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    setFormError(null);
    setConflict(false);

    const found: FieldErrors = validateCategory({ name, slug, description, sortOrder });
    setErrors(found);
    const firstInvalid = FIELD_ORDER.find((field: FieldName) => found[field] !== undefined);
    if (firstInvalid) {
      focusField(form, firstInvalid);
      return;
    }

    const input: CategoryInput = {
      name: name.trim(),
      slug,
      parentId: parentId === '' ? null : parentId,
      description: description.trim() === '' ? null : description,
      sortOrder: Number(sortOrder),
      isActive,
    };
    const callbacks = {
      onSuccess: () => {
        void navigate('/categories');
      },
      onError: (error: Error) => {
        handleError(error, form);
      },
    };
    if (category) {
      update.mutate({ id: category.id, input, version: category.version }, callbacks);
    } else {
      create.mutate(input, callbacks);
    }
  }

  return (
    <form onSubmit={handleSubmit} noValidate aria-labelledby="category-form-title">
      <h2 id="category-form-title">
        {isEdit ? t('categories.editTitle') : t('categories.newTitle')}
      </h2>

      {conflict ? (
        <div role="alert" className="form-error">
          <p>{t('categories.error.versionConflict')}</p>
          <button type="button" onClick={onReload}>
            {t('categories.reload')}
          </button>
        </div>
      ) : null}
      {formError ? (
        <p role="alert" className="form-error">
          {t(formError)}
        </p>
      ) : null}

      <div className="field">
        <label htmlFor="category-name">{t('categories.form.name')}</label>
        <input
          id="category-name"
          name="name"
          type="text"
          autoComplete="off"
          value={name}
          maxLength={NAME_MAX_LENGTH + 50}
          aria-invalid={errors.name ? true : undefined}
          aria-describedby={describedBy(errors.name !== undefined && 'category-name-error')}
          onChange={(event) => {
            setName(event.target.value);
            if (!slugEdited) {
              setSlug(slugFromName(event.target.value));
            }
          }}
        />
        {errors.name ? (
          <p id="category-name-error" className="field-error">
            {t(errors.name)}
          </p>
        ) : null}
      </div>

      <div className="field">
        <label htmlFor="category-slug">{t('categories.form.slug')}</label>
        <input
          id="category-slug"
          name="slug"
          type="text"
          autoComplete="off"
          autoCapitalize="none"
          spellCheck={false}
          value={slug}
          maxLength={SLUG_MAX_LENGTH + 50}
          aria-invalid={errors.slug ? true : undefined}
          aria-describedby={describedBy(
            'category-slug-hint',
            errors.slug !== undefined && 'category-slug-error',
          )}
          onChange={(event) => {
            setSlugEdited(true);
            setSlug(event.target.value);
          }}
        />
        <p id="category-slug-hint" className="field-hint">
          {isEdit ? t('categories.form.slugHintEdit') : t('categories.form.slugHint')}
        </p>
        {errors.slug ? (
          <p id="category-slug-error" className="field-error">
            {t(errors.slug)}
          </p>
        ) : null}
      </div>

      <div className="field">
        <label htmlFor="category-parent">{t('categories.form.parent')}</label>
        <select
          id="category-parent"
          name="parentId"
          value={parentId}
          disabled={hasChildren}
          aria-invalid={errors.parentId ? true : undefined}
          aria-describedby={describedBy(
            hasChildren && 'category-parent-hint',
            errors.parentId !== undefined && 'category-parent-error',
          )}
          onChange={(event) => {
            setParentId(event.target.value);
          }}
        >
          <option value="">{t('categories.form.noParent')}</option>
          {parentOptions.map((option) => (
            <option key={option.id} value={option.id}>
              {option.name}
            </option>
          ))}
        </select>
        {hasChildren ? (
          <p id="category-parent-hint" className="field-hint">
            {t('categories.form.parentLocked')}
          </p>
        ) : null}
        {errors.parentId ? (
          <p id="category-parent-error" className="field-error">
            {t(errors.parentId)}
          </p>
        ) : null}
      </div>

      <div className="field">
        <label htmlFor="category-description">{t('categories.form.description')}</label>
        <textarea
          id="category-description"
          name="description"
          rows={4}
          value={description}
          maxLength={DESCRIPTION_MAX_LENGTH + 100}
          aria-invalid={errors.description ? true : undefined}
          aria-describedby={describedBy(
            errors.description !== undefined && 'category-description-error',
          )}
          onChange={(event) => {
            setDescription(event.target.value);
          }}
        />
        {errors.description ? (
          <p id="category-description-error" className="field-error">
            {t(errors.description)}
          </p>
        ) : null}
      </div>

      <div className="field">
        <label htmlFor="category-sort-order">{t('categories.form.sortOrder')}</label>
        <input
          id="category-sort-order"
          name="sortOrder"
          type="text"
          inputMode="numeric"
          autoComplete="off"
          value={sortOrder}
          aria-invalid={errors.sortOrder ? true : undefined}
          aria-describedby={describedBy(
            'category-sort-order-hint',
            errors.sortOrder !== undefined && 'category-sort-order-error',
          )}
          onChange={(event) => {
            setSortOrder(event.target.value);
          }}
        />
        <p id="category-sort-order-hint" className="field-hint">
          {t('categories.form.sortOrderHint')}
        </p>
        {errors.sortOrder ? (
          <p id="category-sort-order-error" className="field-error">
            {t(errors.sortOrder)}
          </p>
        ) : null}
      </div>

      {isEdit ? (
        <p>
          {category.isActive
            ? t('categories.form.statusActive')
            : t('categories.form.statusInactive')}
        </p>
      ) : (
        <div className="field-check">
          <input
            id="category-active"
            name="isActive"
            type="checkbox"
            checked={isActive}
            onChange={(event) => {
              setIsActive(event.target.checked);
            }}
          />
          <label htmlFor="category-active">{t('categories.form.active')}</label>
        </div>
      )}

      <div className="form-actions">
        <button type="submit" disabled={save.isPending}>
          {save.isPending ? t('categories.form.saving') : t('categories.form.save')}
        </button>
        <Link className="button secondary" to="/categories">
          {t('categories.form.cancel')}
        </Link>
      </div>
    </form>
  );
}

/** Creates a category, or edits the one in the address (`/categories/:id`). */
export function CategoryFormPage() {
  const { id } = useParams();
  // No refetch in the background: the form keeps the version it was loaded with until the administrator reloads.
  const categories = useAdminCategories({ refetchInBackground: false });

  if (categories.isPending) {
    return <p role="status">{t('categories.loading')}</p>;
  }
  if (categories.isError) {
    return (
      <>
        <p role="alert" className="form-error">
          {t('categories.error.loadFailed')}
        </p>
        <button
          type="button"
          onClick={() => {
            void categories.refetch();
          }}
        >
          {t('auth.retry')}
        </button>
      </>
    );
  }

  const category = id === undefined ? undefined : categories.data.find((item) => item.id === id);
  if (id !== undefined && category === undefined) {
    return (
      <>
        <p role="alert" className="form-error">
          {t('categories.error.notFound')}
        </p>
        <Link to="/categories">{t('categories.backToList')}</Link>
      </>
    );
  }

  return (
    <CategoryForm
      // A new version after "Reload" starts the form again with what the server has now.
      key={category ? `${category.id}:${String(category.version)}` : 'new'}
      category={category}
      categories={categories.data}
      onReload={() => {
        void categories.refetch();
      }}
    />
  );
}
