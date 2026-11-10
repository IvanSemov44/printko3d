import {
  ApiError,
  type AdminCategory,
  type AdminProduct,
  type ProductInput,
} from '@storefront/shared';
import { useState, type SubmitEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { FormField } from '../../components/FormField';
import { focusField, useFormValues } from '../../components/useFormValues';
import { t, type MessageKey } from '../../i18n';
import { buildCategoryTree } from '../categories/categoryTree';
import { SLUG_MAX_LENGTH, slugFromName } from '../categories/slug';
import { useAdminCategories } from '../categories/useCategories';
import { FULFILMENT_KEYS, FULFILMENT_TYPES, STATUS_KEYS } from './productLabels';
import { ProductStatusSection } from './ProductStatusSection';
import {
  NAME_MAX_LENGTH,
  SEO_DESCRIPTION_MAX_LENGTH,
  SEO_TITLE_MAX_LENGTH,
  SHORT_DESCRIPTION_MAX_LENGTH,
  parseWholeNumber,
  validateProduct,
  type ProductFormErrors,
} from './productValidation';
import { useAdminProduct, useCreateProduct, useStoreMoney, useUpdateProduct } from './useProducts';
import { VariantsSection } from './VariantsSection';

const FIELD_ORDER = [
  'categoryId',
  'name',
  'slug',
  'shortDescription',
  'description',
  'fulfilmentType',
  'leadTimeDays',
  'seoTitle',
  'seoDescription',
] as const;

function message(key: MessageKey | undefined): string | undefined {
  return key === undefined ? undefined : t(key);
}

/** The categories in the order of the tree, a subcategory after its parent and with the parent's name in front. */
function categoryOptions(categories: readonly AdminCategory[]) {
  return buildCategoryTree(categories).flatMap((node) => [
    { id: node.category.id, label: node.category.name },
    ...node.children.map((child) => ({
      id: child.id,
      label: `${node.category.name} / ${child.name}`,
    })),
  ]);
}

/** A product is hidden from the shop when its category, or the parent of its category, is not active. */
function isCategoryHidden(categories: readonly AdminCategory[], categoryId: string): boolean {
  const category = categories.find((item) => item.id === categoryId);
  if (!category) {
    return false;
  }
  const parent = categories.find((item) => item.id === category.parentId);
  return !category.isActive || (parent !== undefined && !parent.isActive);
}

interface FormProps {
  product: AdminProduct | undefined;
  categories: readonly AdminCategory[];
  onReload: () => void;
}

function ProductForm({ product, categories, onReload }: FormProps) {
  const navigate = useNavigate();
  const create = useCreateProduct();
  const update = useUpdateProduct();
  const save = product ? update : create;
  const isEdit = product !== undefined;

  const { values, setValue } = useFormValues({
    categoryId: product?.categoryId ?? '',
    name: product?.name ?? '',
    slug: product?.slug ?? '',
    shortDescription: product?.shortDescription ?? '',
    description: product?.description ?? '',
    fulfilmentType: product?.fulfilmentType ?? ('InStock' as const),
    leadTimeDays:
      product?.leadTimeDays === null || product === undefined ? '' : String(product.leadTimeDays),
    seoTitle: product?.seoTitle ?? '',
    seoDescription: product?.seoDescription ?? '',
  });
  // The slug follows the name only for a new product, and only until the administrator edits it.
  const [slugEdited, setSlugEdited] = useState(isEdit);
  const [variantStock, setVariantStock] = useState<Record<string, string>>({});
  const [errors, setErrors] = useState<ProductFormErrors>({});
  const [stockErrors, setStockErrors] = useState<Record<string, MessageKey>>({});
  const [formError, setFormError] = useState<MessageKey | null>(null);
  const [conflict, setConflict] = useState(false);

  const variants = product?.variants ?? [];
  // A made-to-order product has no stock, so when it becomes in-stock the stock of every variant must be given.
  const needsStock =
    product?.fulfilmentType === 'MadeToOrder' &&
    values.fulfilmentType === 'InStock' &&
    variants.length > 0;
  const clearsStock =
    product?.fulfilmentType === 'InStock' &&
    values.fulfilmentType === 'MadeToOrder' &&
    variants.length > 0;
  const slugChangeBreaksLinks = product?.status === 'Published' && values.slug !== product.slug;
  const options = categoryOptions(categories);

  function handleError(error: Error, form: HTMLFormElement) {
    const code = error instanceof ApiError ? error.code : null;
    if (error instanceof ApiError && error.status === 409 && code === 'product.slug_taken') {
      setErrors({ slug: 'products.error.slugTaken' });
      focusField(form, 'slug');
    } else if (error instanceof ApiError && error.status === 409) {
      setConflict(true);
    } else if (error instanceof ApiError && code === 'product.category_not_found') {
      setErrors({ categoryId: 'products.error.categoryNotFound' });
      focusField(form, 'categoryId');
    } else if (error instanceof ApiError && error.status === 400) {
      setFormError('products.error.rejected');
    } else {
      setFormError('products.error.saveFailed');
    }
  }

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    setFormError(null);
    setConflict(false);

    const found = validateProduct(values);
    setErrors(found);

    const stock: Record<string, number> = {};
    const stockProblems: Record<string, MessageKey> = {};
    if (needsStock) {
      for (const variant of variants) {
        const quantity = parseWholeNumber(variantStock[variant.id] ?? '');
        if (quantity === null) {
          stockProblems[variant.id] = 'products.error.variantStockInvalid';
        } else {
          stock[variant.id] = quantity;
        }
      }
    }
    setStockErrors(stockProblems);

    const firstInvalid = FIELD_ORDER.find((field) => found[field] !== undefined);
    if (firstInvalid) {
      focusField(form, firstInvalid);
      return;
    }
    const firstBadStock = variants.find((variant) => variant.id in stockProblems);
    if (firstBadStock) {
      focusField(form, `variantStock-${firstBadStock.id}`);
      return;
    }

    const input: ProductInput = {
      categoryId: values.categoryId,
      name: values.name.trim(),
      slug: values.slug,
      shortDescription: values.shortDescription,
      description: values.description,
      fulfilmentType: values.fulfilmentType,
      leadTimeDays:
        values.fulfilmentType === 'MadeToOrder' ? parseWholeNumber(values.leadTimeDays) : null,
      seoTitle: values.seoTitle.trim() === '' ? null : values.seoTitle,
      seoDescription: values.seoDescription.trim() === '' ? null : values.seoDescription,
      ...(needsStock ? { variantStock: stock } : {}),
    };
    const onError = (error: Error) => {
      handleError(error, form);
    };
    if (product) {
      update.mutate(
        { id: product.id, input, version: product.version },
        {
          onSuccess: () => {
            void navigate('/products');
          },
          onError,
        },
      );
    } else {
      create.mutate(input, {
        // A new product has no variants yet, so the next step is its own page, where they are added.
        onSuccess: (created) => {
          void navigate(`/products/${created.id}`);
        },
        onError,
      });
    }
  }

  return (
    <form onSubmit={handleSubmit} noValidate aria-labelledby="product-form-title">
      <h2 id="product-form-title">
        {isEdit ? t('products.editTitle') : t('products.newTitle')}
        {product ? (
          <>
            {' '}
            <span className={`badge status-${product.status}`}>
              {t(STATUS_KEYS[product.status])}
            </span>
          </>
        ) : null}
      </h2>

      {conflict ? (
        <div role="alert" className="form-error">
          <p>{t('products.error.versionConflict')}</p>
          <button type="button" onClick={onReload}>
            {t('products.reload')}
          </button>
        </div>
      ) : null}
      {formError ? (
        <p role="alert" className="form-error">
          {t(formError)}
        </p>
      ) : null}

      <FormField
        id="product-category"
        name="categoryId"
        label={t('products.form.category')}
        hint={
          isCategoryHidden(categories, values.categoryId)
            ? t('products.form.inactiveCategory')
            : undefined
        }
        error={message(errors.categoryId)}
      >
        {(control) => (
          <select
            {...control}
            value={values.categoryId}
            onChange={(event) => {
              setValue('categoryId', event.target.value);
            }}
          >
            <option value="">{t('products.form.chooseCategory')}</option>
            {options.map((option) => (
              <option key={option.id} value={option.id}>
                {option.label}
              </option>
            ))}
          </select>
        )}
      </FormField>

      <FormField
        id="product-name"
        name="name"
        label={t('products.form.name')}
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
        id="product-slug"
        name="slug"
        label={t('products.form.slug')}
        hint={
          slugChangeBreaksLinks
            ? t('products.form.slugPublishedWarning')
            : isEdit
              ? t('products.form.slugHintEdit')
              : t('products.form.slugHint')
        }
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
        id="product-short-description"
        name="shortDescription"
        label={t('products.form.shortDescription')}
        hint={t('products.form.shortDescriptionHint')}
        error={message(errors.shortDescription)}
      >
        {(control) => (
          <textarea
            {...control}
            rows={2}
            value={values.shortDescription}
            maxLength={SHORT_DESCRIPTION_MAX_LENGTH + 100}
            onChange={(event) => {
              setValue('shortDescription', event.target.value);
            }}
          />
        )}
      </FormField>

      <FormField
        id="product-description"
        name="description"
        label={t('products.form.description')}
        error={message(errors.description)}
      >
        {(control) => (
          <textarea
            {...control}
            rows={6}
            value={values.description}
            onChange={(event) => {
              setValue('description', event.target.value);
            }}
          />
        )}
      </FormField>

      <FormField
        id="product-fulfilment"
        name="fulfilmentType"
        label={t('products.form.fulfilmentType')}
        hint={
          needsStock
            ? t('products.form.toInStockHint')
            : clearsStock
              ? t('products.form.toMadeToOrderHint')
              : undefined
        }
        error={message(errors.fulfilmentType)}
      >
        {(control) => (
          <select
            {...control}
            value={values.fulfilmentType}
            onChange={(event) => {
              const chosen = FULFILMENT_TYPES.find((type) => type === event.target.value);
              if (chosen) {
                setValue('fulfilmentType', chosen);
              }
            }}
          >
            {FULFILMENT_TYPES.map((type) => (
              <option key={type} value={type}>
                {type === 'InStock'
                  ? t('products.form.fulfilmentInStock')
                  : t('products.form.fulfilmentMadeToOrder')}
              </option>
            ))}
          </select>
        )}
      </FormField>
      <p className="visually-hidden">{t(FULFILMENT_KEYS[values.fulfilmentType])}</p>

      {values.fulfilmentType === 'MadeToOrder' ? (
        <FormField
          id="product-lead-time"
          name="leadTimeDays"
          label={t('products.form.leadTimeDays')}
          hint={t('products.form.leadTimeHint')}
          error={message(errors.leadTimeDays)}
        >
          {(control) => (
            <input
              {...control}
              type="text"
              inputMode="numeric"
              autoComplete="off"
              value={values.leadTimeDays}
              onChange={(event) => {
                setValue('leadTimeDays', event.target.value);
              }}
            />
          )}
        </FormField>
      ) : null}

      {needsStock
        ? variants.map((variant) => (
            <FormField
              key={variant.id}
              id={`product-variant-stock-${variant.id}`}
              name={`variantStock-${variant.id}`}
              label={t('products.form.variantStock', { name: variant.name })}
              error={message(stockErrors[variant.id])}
            >
              {(control) => (
                <input
                  {...control}
                  type="text"
                  inputMode="numeric"
                  autoComplete="off"
                  value={variantStock[variant.id] ?? ''}
                  onChange={(event) => {
                    setVariantStock((current) => ({
                      ...current,
                      [variant.id]: event.target.value,
                    }));
                  }}
                />
              )}
            </FormField>
          ))
        : null}

      <FormField
        id="product-seo-title"
        name="seoTitle"
        label={t('products.form.seoTitle')}
        hint={t('products.form.seoTitleHint')}
        error={message(errors.seoTitle)}
      >
        {(control) => (
          <input
            {...control}
            type="text"
            autoComplete="off"
            value={values.seoTitle}
            maxLength={SEO_TITLE_MAX_LENGTH + 50}
            onChange={(event) => {
              setValue('seoTitle', event.target.value);
            }}
          />
        )}
      </FormField>

      <FormField
        id="product-seo-description"
        name="seoDescription"
        label={t('products.form.seoDescription')}
        hint={t('products.form.seoDescriptionHint')}
        error={message(errors.seoDescription)}
      >
        {(control) => (
          <textarea
            {...control}
            rows={3}
            value={values.seoDescription}
            maxLength={SEO_DESCRIPTION_MAX_LENGTH + 100}
            onChange={(event) => {
              setValue('seoDescription', event.target.value);
            }}
          />
        )}
      </FormField>

      <div className="form-actions">
        <button type="submit" disabled={save.isPending}>
          {save.isPending ? t('products.form.saving') : t('products.form.save')}
        </button>
        <Link className="button secondary" to="/products">
          {t('products.form.cancel')}
        </Link>
      </div>
    </form>
  );
}

/** Creates a product, or edits the one in the address (`/products/:id`) with its status and variants. */
export function ProductFormPage() {
  const { id } = useParams();
  // No refetch in the background: the form keeps the version it was loaded with until the administrator reloads.
  const categories = useAdminCategories({ refetchInBackground: false });
  const product = useAdminProduct(id);
  const money = useStoreMoney();
  // Raised by "Reload": the form starts again with what the server has now.
  const [generation, setGeneration] = useState(0);

  const loading =
    categories.isPending || money.isPending || (id !== undefined && product.isPending);
  if (loading && !categories.isError && !money.isError && !product.isError) {
    return <p role="status">{t('products.loading')}</p>;
  }
  if (product.error instanceof ApiError && product.error.status === 404) {
    return (
      <>
        <p role="alert" className="form-error">
          {t('products.error.notFound')}
        </p>
        <Link to="/products">{t('products.backToList')}</Link>
      </>
    );
  }
  if (categories.isError || money.isError || product.isError || !categories.data || !money.data) {
    return (
      <>
        <p role="alert" className="form-error">
          {t('products.error.loadFailed')}
        </p>
        <button
          type="button"
          onClick={() => {
            void categories.refetch();
            void money.refetch();
            void product.refetch();
          }}
        >
          {t('auth.retry')}
        </button>
      </>
    );
  }

  const current = id === undefined ? undefined : product.data;
  const reload = () => {
    void product.refetch().then(() => {
      setGeneration((value) => value + 1);
    });
  };

  return (
    <>
      <ProductForm
        key={`${id ?? 'new'}:${String(generation)}`}
        product={current}
        categories={categories.data}
        onReload={reload}
      />
      {current ? (
        <>
          <ProductStatusSection product={current} onReload={reload} />
          <VariantsSection product={current} money={money.data} />
        </>
      ) : null}
    </>
  );
}
