import {
  ApiError,
  IMAGE_ALT_MAX_LENGTH,
  type AdminImage,
  type AdminProduct,
} from '@storefront/shared';
import { useEffect, useRef, useState, type SubmitEvent } from 'react';
import { FormField } from '../../components/FormField';
import { focusField, useFormValues } from '../../components/useFormValues';
import { t, type MessageKey } from '../../i18n';
import { validateAltText } from './imageValidation';
import { useUpdateImage } from './useProducts';

export interface ImageEditFormProps {
  product: AdminProduct;
  image: AdminImage;
  onDone: () => void;
}

/** Changes the alt text of an image and the variant it shows. */
export function ImageEditForm({ product, image, onDone }: ImageEditFormProps) {
  const update = useUpdateImage(product.id);
  const form = useRef<HTMLFormElement>(null);
  const { values, setValue } = useFormValues({
    altText: image.altText,
    variantId: image.variantId ?? '',
  });
  const [altError, setAltError] = useState<MessageKey | undefined>(undefined);
  const [formError, setFormError] = useState<MessageKey | null>(null);

  // The form opens with the focus on its first field.
  useEffect(() => {
    if (form.current) {
      focusField(form.current, 'altText');
    }
  }, []);

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const element = event.currentTarget;
    setFormError(null);

    const error = validateAltText(values.altText);
    setAltError(error);
    if (error) {
      focusField(element, 'altText');
      return;
    }

    update.mutate(
      { imageId: image.id, altText: values.altText.trim(), variantId: values.variantId || null },
      {
        onSuccess: onDone,
        onError: (failure) => {
          setFormError(
            failure instanceof ApiError && failure.status === 400
              ? 'images.error.rejected'
              : 'images.error.saveFailed',
          );
        },
      },
    );
  }

  return (
    <form
      ref={form}
      onSubmit={handleSubmit}
      noValidate
      aria-labelledby="image-edit-title"
      className="section"
    >
      <h4 id="image-edit-title">{t('images.edit.title', { alt: image.altText })}</h4>

      {formError ? (
        <p role="alert" className="form-error">
          {t(formError)}
        </p>
      ) : null}

      <FormField
        id="image-edit-alt"
        name="altText"
        label={t('images.upload.altText')}
        hint={t('images.upload.altTextHint')}
        error={altError === undefined ? undefined : t(altError)}
      >
        {(control) => (
          <input
            {...control}
            type="text"
            autoComplete="off"
            value={values.altText}
            maxLength={IMAGE_ALT_MAX_LENGTH + 100}
            onChange={(event) => {
              setValue('altText', event.target.value);
            }}
          />
        )}
      </FormField>

      {product.variants.length > 0 ? (
        <FormField id="image-edit-variant" name="variantId" label={t('images.upload.variant')}>
          {(control) => (
            <select
              {...control}
              value={values.variantId}
              onChange={(event) => {
                setValue('variantId', event.target.value);
              }}
            >
              <option value="">{t('images.wholeProduct')}</option>
              {product.variants.map((variant) => (
                <option key={variant.id} value={variant.id}>
                  {variant.name}
                </option>
              ))}
            </select>
          )}
        </FormField>
      ) : null}

      <div className="form-actions">
        <button type="submit" disabled={update.isPending}>
          {update.isPending ? t('images.edit.saving') : t('images.edit.save')}
        </button>
        <button type="button" className="secondary" onClick={onDone}>
          {t('images.edit.cancel')}
        </button>
      </div>
    </form>
  );
}
