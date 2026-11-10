import { ApiError, IMAGE_ALT_MAX_LENGTH, type AdminProduct } from '@storefront/shared';
import { useRef, useState, type SubmitEvent } from 'react';
import { FormField } from '../../components/FormField';
import { focusField, useFormValues } from '../../components/useFormValues';
import { t, type MessageKey } from '../../i18n';
import { validateImageUpload, type ImageFormErrors } from './imageValidation';
import { useUploadImage } from './useProducts';

const FIELD_ORDER = ['file', 'altText'] as const;

function message(key: MessageKey | undefined): string | undefined {
  return key === undefined ? undefined : t(key);
}

/** What the server's refusal means for the form: a message for the file field, or one for the whole form. */
function describeRefusal(error: Error): { errors: ImageFormErrors } | { formError: MessageKey } {
  if (error instanceof ApiError) {
    // 413 can also come from nginx in front of the API, without a code.
    if (error.status === 413 || error.code === 'image.too_large') {
      return { errors: { file: 'images.error.fileTooLarge' } };
    }
    if (error.code === 'image.type_not_allowed') {
      return { errors: { file: 'images.error.fileType' } };
    }
    if (error.code === 'image.empty') {
      return { errors: { file: 'images.error.fileEmpty' } };
    }
    if (error.code === 'image.limit_reached') {
      return { formError: 'images.error.limitReached' };
    }
    if (error.status === 400) {
      return { formError: 'images.error.rejected' };
    }
  }
  return { formError: 'images.error.uploadFailed' };
}

export interface ImageUploadFormProps {
  product: AdminProduct;
  /** The most images are there already: the form says so and does not send. */
  full: boolean;
}

/** Uploads one image. Type and size are checked here first; the server still decides. */
export function ImageUploadForm({ product, full }: ImageUploadFormProps) {
  const upload = useUploadImage(product.id);
  const form = useRef<HTMLFormElement>(null);
  const fileInput = useRef<HTMLInputElement>(null);
  const { values, setValue } = useFormValues({ altText: '', variantId: '' });
  const [errors, setErrors] = useState<ImageFormErrors>({});
  const [formError, setFormError] = useState<MessageKey | null>(null);
  const [done, setDone] = useState(false);

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const element = event.currentTarget;
    setFormError(null);
    setDone(false);

    const file = fileInput.current?.files?.[0];
    const found = validateImageUpload(file, values.altText);
    if (found.file !== undefined || found.altText !== undefined || !file) {
      setErrors(found);
      const firstInvalid = FIELD_ORDER.find((name) => found[name] !== undefined);
      if (firstInvalid) {
        focusField(element, firstInvalid);
      }
      return;
    }
    setErrors({});

    upload.mutate(
      { file, altText: values.altText.trim(), variantId: values.variantId || null },
      {
        onSuccess: () => {
          setDone(true);
          setValue('altText', '');
          setValue('variantId', '');
          element.reset();
          focusField(element, 'file');
        },
        onError: (error) => {
          const refusal = describeRefusal(error);
          if ('errors' in refusal) {
            setErrors(refusal.errors);
            focusField(element, 'file');
          } else {
            setFormError(refusal.formError);
          }
        },
      },
    );
  }

  return (
    <form
      ref={form}
      onSubmit={handleSubmit}
      noValidate
      aria-labelledby="image-upload-title"
      className="image-upload"
    >
      <h4 id="image-upload-title">{t('images.upload.title')}</h4>

      {/* Always in the page, so the screen reader announces the change when the text appears. */}
      <p role="status" className={upload.isPending || done ? 'field-hint' : 'visually-hidden'}>
        {upload.isPending ? t('images.upload.uploading') : done ? t('images.upload.done') : ''}
      </p>

      {full ? <p className="warning">{t('images.error.limitReached')}</p> : null}
      {formError ? (
        <p role="alert" className="form-error">
          {t(formError)}
        </p>
      ) : null}

      <FormField
        id="image-file"
        name="file"
        label={t('images.upload.file')}
        hint={t('images.upload.fileHint')}
        error={message(errors.file)}
      >
        {(control) => (
          <input
            {...control}
            ref={fileInput}
            type="file"
            accept="image/jpeg,image/png,image/webp"
            disabled={full}
          />
        )}
      </FormField>

      <FormField
        id="image-alt"
        name="altText"
        label={t('images.upload.altText')}
        hint={t('images.upload.altTextHint')}
        error={message(errors.altText)}
      >
        {(control) => (
          <input
            {...control}
            type="text"
            autoComplete="off"
            value={values.altText}
            maxLength={IMAGE_ALT_MAX_LENGTH + 100}
            disabled={full}
            onChange={(event) => {
              setValue('altText', event.target.value);
            }}
          />
        )}
      </FormField>

      {product.variants.length > 0 ? (
        <FormField id="image-variant" name="variantId" label={t('images.upload.variant')}>
          {(control) => (
            <select
              {...control}
              value={values.variantId}
              disabled={full}
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
        <button type="submit" disabled={upload.isPending || full}>
          {upload.isPending ? t('images.upload.uploading') : t('images.upload.submit')}
        </button>
      </div>
    </form>
  );
}
