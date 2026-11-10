import {
  ApiError,
  currencyFractionDigits,
  formatMoneyInput,
  type AdminProduct,
  type AdminVariant,
  type StoreMoneySettings,
} from '@storefront/shared';
import { useEffect, useRef, useState, type SubmitEvent } from 'react';
import { FormField } from '../../components/FormField';
import { focusField, useFormValues } from '../../components/useFormValues';
import { t, type MessageKey } from '../../i18n';
import { useAddVariant, useUpdateVariant } from './useProducts';
import {
  ATTRIBUTE_NAME_MAX_LENGTH,
  SKU_MAX_LENGTH,
  VARIANT_NAME_MAX_LENGTH,
  formatAttributes,
  validateVariant,
  type VariantFormErrors,
} from './variantValidation';

const FIELD_ORDER = [
  'sku',
  'name',
  'price',
  'compareAtPrice',
  'weightGrams',
  'stockQuantity',
  'attributes',
] as const;

function message(key: MessageKey | undefined): string | undefined {
  return key === undefined ? undefined : t(key);
}

export interface VariantFormProps {
  product: AdminProduct;
  /** The variant to edit, or undefined for a new one. */
  variant: AdminVariant | undefined;
  money: StoreMoneySettings;
  /** Where a new variant goes: after the last one. */
  nextSortOrder: number;
  onDone: () => void;
}

/** Adds a variant, or edits one. Prices are typed in the main unit of the shop currency and sent in the smallest one. */
export function VariantForm({ product, variant, money, nextSortOrder, onDone }: VariantFormProps) {
  const add = useAddVariant(product.id);
  const update = useUpdateVariant(product.id);
  const save = variant ? update : add;
  const fractionDigits = currencyFractionDigits(money.currency, money.locale);
  const isInStock = product.fulfilmentType === 'InStock';
  const form = useRef<HTMLFormElement>(null);

  const { values, setValue } = useFormValues({
    sku: variant?.sku ?? '',
    name: variant?.name ?? '',
    price: variant ? formatMoneyInput(variant.price, fractionDigits) : '',
    compareAtPrice:
      variant?.compareAtPrice != null
        ? formatMoneyInput(variant.compareAtPrice, fractionDigits)
        : '',
    weightGrams: String(variant?.weightGrams ?? 0),
    stockQuantity: variant?.stockQuantity == null ? '' : String(variant.stockQuantity),
    attributes: variant ? formatAttributes(variant.attributes) : '',
  });
  const [errors, setErrors] = useState<VariantFormErrors>({});
  const [formError, setFormError] = useState<MessageKey | null>(null);

  // The form opens with the focus on its first field.
  useEffect(() => {
    if (form.current) {
      focusField(form.current, 'sku');
    }
  }, []);

  function handleError(error: Error, element: HTMLFormElement) {
    if (error instanceof ApiError && error.status === 409 && error.code === 'variant.sku_taken') {
      setErrors({ sku: 'variants.error.skuTaken' });
      focusField(element, 'sku');
    } else if (error instanceof ApiError && error.status === 400) {
      setFormError('variants.error.rejected');
    } else {
      setFormError('variants.error.saveFailed');
    }
  }

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const element = event.currentTarget;
    setFormError(null);

    const result = validateVariant(values, {
      fulfilmentType: product.fulfilmentType,
      fractionDigits,
    });
    if (!result.ok) {
      setErrors(result.errors);
      const firstInvalid = FIELD_ORDER.find((field) => result.errors[field] !== undefined);
      if (firstInvalid) {
        focusField(element, firstInvalid);
      }
      return;
    }
    setErrors({});

    const callbacks = {
      onSuccess: onDone,
      onError: (error: Error) => {
        handleError(error, element);
      },
    };
    if (variant) {
      update.mutate({ variantId: variant.id, input: result.input }, callbacks);
    } else {
      add.mutate({ input: result.input, sortOrder: nextSortOrder }, callbacks);
    }
  }

  return (
    <form
      ref={form}
      onSubmit={handleSubmit}
      noValidate
      aria-labelledby="variant-form-title"
      className="section"
    >
      <h3 id="variant-form-title">
        {variant ? t('variants.editTitle', { name: variant.name }) : t('variants.newTitle')}
      </h3>

      {formError ? (
        <p role="alert" className="form-error">
          {t(formError)}
        </p>
      ) : null}

      <FormField
        id="variant-sku"
        name="sku"
        label={t('variants.form.sku')}
        hint={t('variants.form.skuHint')}
        error={message(errors.sku)}
      >
        {(control) => (
          <input
            {...control}
            type="text"
            autoComplete="off"
            autoCapitalize="characters"
            spellCheck={false}
            value={values.sku}
            maxLength={SKU_MAX_LENGTH + 20}
            onChange={(event) => {
              setValue('sku', event.target.value);
            }}
          />
        )}
      </FormField>

      <FormField
        id="variant-name"
        name="name"
        label={t('variants.form.name')}
        error={message(errors.name)}
      >
        {(control) => (
          <input
            {...control}
            type="text"
            autoComplete="off"
            value={values.name}
            maxLength={VARIANT_NAME_MAX_LENGTH + 50}
            onChange={(event) => {
              setValue('name', event.target.value);
            }}
          />
        )}
      </FormField>

      <FormField
        id="variant-price"
        name="price"
        label={t('variants.form.price', { currency: money.currency })}
        hint={t('variants.form.priceHint')}
        error={message(errors.price)}
      >
        {(control) => (
          <input
            {...control}
            type="text"
            inputMode="decimal"
            autoComplete="off"
            value={values.price}
            onChange={(event) => {
              setValue('price', event.target.value);
            }}
          />
        )}
      </FormField>

      <FormField
        id="variant-compare-at-price"
        name="compareAtPrice"
        label={t('variants.form.compareAtPrice', { currency: money.currency })}
        hint={t('variants.form.compareAtPriceHint')}
        error={message(errors.compareAtPrice)}
      >
        {(control) => (
          <input
            {...control}
            type="text"
            inputMode="decimal"
            autoComplete="off"
            value={values.compareAtPrice}
            onChange={(event) => {
              setValue('compareAtPrice', event.target.value);
            }}
          />
        )}
      </FormField>

      <FormField
        id="variant-weight"
        name="weightGrams"
        label={t('variants.form.weightGrams')}
        error={message(errors.weightGrams)}
      >
        {(control) => (
          <input
            {...control}
            type="text"
            inputMode="numeric"
            autoComplete="off"
            value={values.weightGrams}
            onChange={(event) => {
              setValue('weightGrams', event.target.value);
            }}
          />
        )}
      </FormField>

      {isInStock ? (
        <FormField
          id="variant-stock"
          name="stockQuantity"
          label={t('variants.form.stockQuantity')}
          error={message(errors.stockQuantity)}
        >
          {(control) => (
            <input
              {...control}
              type="text"
              inputMode="numeric"
              autoComplete="off"
              value={values.stockQuantity}
              onChange={(event) => {
                setValue('stockQuantity', event.target.value);
              }}
            />
          )}
        </FormField>
      ) : null}

      <FormField
        id="variant-attributes"
        name="attributes"
        label={t('variants.form.attributes')}
        hint={t('variants.form.attributesHint')}
        error={message(errors.attributes)}
      >
        {(control) => (
          <textarea
            {...control}
            rows={4}
            spellCheck={false}
            value={values.attributes}
            maxLength={10 * (ATTRIBUTE_NAME_MAX_LENGTH + 120)}
            onChange={(event) => {
              setValue('attributes', event.target.value);
            }}
          />
        )}
      </FormField>

      <div className="form-actions">
        <button type="submit" disabled={save.isPending}>
          {save.isPending ? t('variants.form.saving') : t('variants.form.save')}
        </button>
        <button type="button" className="secondary" onClick={onDone}>
          {t('variants.form.cancel')}
        </button>
      </div>
    </form>
  );
}
