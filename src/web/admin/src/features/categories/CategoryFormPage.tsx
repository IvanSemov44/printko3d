import { ApiError, type AdminCategory, type CategoryInput } from '@storefront/shared';
import { useState, type SubmitEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { FormField } from '../../components/FormField';
import { focusField, useFormValues } from '../../components/useFormValues';
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

/** The translated message of a field, or undefined when the field has no error. */
function message(key: MessageKey | undefined): string | undefined {
  return key === undefined ? undefined : t(key);
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
  const { values, setValue } = useFormValues({
    name: category?.name ?? '',
    slug: category?.slug ?? '',
    parentId: category?.parentId ?? '',
    description: category?.description ?? '',
    sortOrder: String(category?.sortOrder ?? nextSortOrder),
  });
  // The slug follows the name only for a new category, and only until the administrator edits it.
  const [slugEdited, setSlugEdited] = useState(isEdit);
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

    const found: FieldErrors = validateCategory(values);
    setErrors(found);
    const firstInvalid = FIELD_ORDER.find((field: FieldName) => found[field] !== undefined);
    if (firstInvalid) {
      focusField(form, firstInvalid);
      return;
    }

    const input: CategoryInput = {
      name: values.name.trim(),
      slug: values.slug,
      parentId: values.parentId === '' ? null : values.parentId,
      description: values.description.trim() === '' ? null : values.description,
      sortOrder: Number(values.sortOrder),
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

      <FormField
        id="category-name"
        name="name"
        label={t('categories.form.name')}
        error={message(errors.name)}
      >
        {(control) => (
          <input
            {...control}
            type="text"
            autoComplete="off"
            value={values.name}
            maxLength={NAME_MAX_LENGTH + 50}
            onChange={(event) => {
              setValue('name', event.target.value);
              if (!slugEdited) {
                setValue('slug', slugFromName(event.target.value));
              }
            }}
          />
        )}
      </FormField>

      <FormField
        id="category-slug"
        name="slug"
        label={t('categories.form.slug')}
        hint={isEdit ? t('categories.form.slugHintEdit') : t('categories.form.slugHint')}
        error={message(errors.slug)}
      >
        {(control) => (
          <input
            {...control}
            type="text"
            autoComplete="off"
            autoCapitalize="none"
            spellCheck={false}
            value={values.slug}
            maxLength={SLUG_MAX_LENGTH + 50}
            onChange={(event) => {
              setSlugEdited(true);
              setValue('slug', event.target.value);
            }}
          />
        )}
      </FormField>

      <FormField
        id="category-parent"
        name="parentId"
        label={t('categories.form.parent')}
        hint={hasChildren ? t('categories.form.parentLocked') : undefined}
        error={message(errors.parentId)}
      >
        {(control) => (
          <select
            {...control}
            value={values.parentId}
            disabled={hasChildren}
            onChange={(event) => {
              setValue('parentId', event.target.value);
            }}
          >
            <option value="">{t('categories.form.noParent')}</option>
            {parentOptions.map((option) => (
              <option key={option.id} value={option.id}>
                {option.name}
              </option>
            ))}
          </select>
        )}
      </FormField>

      <FormField
        id="category-description"
        name="description"
        label={t('categories.form.description')}
        error={message(errors.description)}
      >
        {(control) => (
          <textarea
            {...control}
            rows={4}
            value={values.description}
            maxLength={DESCRIPTION_MAX_LENGTH + 100}
            onChange={(event) => {
              setValue('description', event.target.value);
            }}
          />
        )}
      </FormField>

      <FormField
        id="category-sort-order"
        name="sortOrder"
        label={t('categories.form.sortOrder')}
        hint={t('categories.form.sortOrderHint')}
        error={message(errors.sortOrder)}
      >
        {(control) => (
          <input
            {...control}
            type="text"
            inputMode="numeric"
            autoComplete="off"
            value={values.sortOrder}
            onChange={(event) => {
              setValue('sortOrder', event.target.value);
            }}
          />
        )}
      </FormField>

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
