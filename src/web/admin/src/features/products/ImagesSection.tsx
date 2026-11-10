import {
  ApiError,
  IMAGE_MAX_COUNT,
  imageUrl,
  type AdminImage,
  type AdminProduct,
} from '@storefront/shared';
import { useEffect, useRef, useState } from 'react';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { t, type MessageKey } from '../../i18n';
import { ImageEditForm } from './ImageEditForm';
import { ImageUploadForm } from './ImageUploadForm';
import { useDeleteImage, useImageBaseUrl, useMoveImage, useSetPrimaryImage } from './useProducts';

function messageForActionError(error: Error): MessageKey {
  return error instanceof ApiError && error.status === 404
    ? 'images.error.notFound'
    : 'images.error.actionFailed';
}

type Direction = 'Up' | 'Down';

/** The picture of one image, or a box with the same size when there is no address or the file does not load. */
function ImageThumb({ url, alt }: { url: string | null; alt: string }) {
  const [failed, setFailed] = useState(false);
  if (url === null || failed) {
    return (
      <div className="image-thumb image-placeholder" role="img" aria-label={alt}>
        <span aria-hidden="true">{t('images.noPreview')}</span>
      </div>
    );
  }
  return (
    <img
      className="image-thumb"
      src={url}
      alt={alt}
      width={96}
      height={96}
      loading="lazy"
      onError={() => {
        setFailed(true);
      }}
    />
  );
}

interface RowProps {
  image: AdminImage;
  product: AdminProduct;
  imageBaseUrl: string | null;
  position: number;
  count: number;
  busy: boolean;
  onMove: (image: AdminImage, direction: Direction) => void;
  onPrimary: (image: AdminImage) => void;
  onEdit: (image: AdminImage) => void;
  onDelete: (image: AdminImage) => void;
}

function ImageRow({
  image,
  product,
  imageBaseUrl,
  position,
  count,
  busy,
  onMove,
  onPrimary,
  onEdit,
  onDelete,
}: RowProps) {
  const alt = { alt: image.altText };
  const variant = image.variantId
    ? product.variants.find((candidate) => candidate.id === image.variantId)
    : undefined;

  return (
    <div className={image.isPrimary ? 'category-row image-row primary' : 'category-row image-row'}>
      <ImageThumb url={imageUrl(imageBaseUrl, image.storageKey)} alt={image.altText} />
      <div className="category-info">
        <strong>{image.altText}</strong>
        {image.isPrimary ? <span className="badge">{t('images.primary')}</span> : null}
        <span className="category-meta">
          {variant ? t('images.forVariant', { name: variant.name }) : t('images.wholeProduct')}
        </span>
      </div>
      <div className="category-actions">
        <button
          type="button"
          id={`image-${image.id}-Up`}
          className="secondary"
          aria-label={t('images.moveUp', alt)}
          disabled={busy || position <= 0}
          onClick={() => {
            onMove(image, 'Up');
          }}
        >
          ↑
        </button>
        <button
          type="button"
          id={`image-${image.id}-Down`}
          className="secondary"
          aria-label={t('images.moveDown', alt)}
          disabled={busy || position >= count - 1}
          onClick={() => {
            onMove(image, 'Down');
          }}
        >
          ↓
        </button>
        <button
          type="button"
          className="secondary"
          aria-label={t('images.setPrimary', alt)}
          disabled={busy || image.isPrimary}
          onClick={() => {
            onPrimary(image);
          }}
        >
          {t('images.setPrimaryShort')}
        </button>
        <button
          type="button"
          className="secondary"
          aria-label={t('images.edit', alt)}
          disabled={busy}
          onClick={() => {
            onEdit(image);
          }}
        >
          {t('images.editShort')}
        </button>
        <button
          type="button"
          className="danger"
          aria-label={t('images.delete', alt)}
          disabled={busy}
          onClick={() => {
            onDelete(image);
          }}
        >
          {t('images.deleteShort')}
        </button>
      </div>
    </div>
  );
}

export interface ImagesSectionProps {
  product: AdminProduct;
}

/** The images of a product: list, upload, change, primary, order and delete. */
export function ImagesSection({ product }: ImagesSectionProps) {
  const imageBaseUrl = useImageBaseUrl();
  const move = useMoveImage(product.id);
  const setPrimary = useSetPrimaryImage(product.id);
  const remove = useDeleteImage(product.id);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<AdminImage | null>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  // The button to give the focus back to after a move: the list is drawn again with the image in its new place.
  const moved = useRef<{ id: string; direction: Direction } | null>(null);

  const images = product.images;
  const busy = move.isPending || setPrimary.isPending || remove.isPending;
  const actionError = move.error ?? setPrimary.error ?? remove.error;
  const editedImage = editingId ? images.find((image) => image.id === editingId) : undefined;

  useEffect(() => {
    const last = moved.current;
    if (!last) {
      return;
    }
    moved.current = null;
    // At the end of the list the button is disabled, so the focus goes to the other arrow.
    const same = document.getElementById(`image-${last.id}-${last.direction}`);
    const other = document.getElementById(
      `image-${last.id}-${last.direction === 'Up' ? 'Down' : 'Up'}`,
    );
    const target = same instanceof HTMLButtonElement && !same.disabled ? same : other;
    if (target instanceof HTMLButtonElement && !target.disabled) {
      target.focus();
    }
  }, [images]);

  function resetActions() {
    move.reset();
    setPrimary.reset();
    remove.reset();
  }

  function handleMove(image: AdminImage, direction: Direction) {
    resetActions();
    moved.current = { id: image.id, direction };
    move.mutate(
      { imageId: image.id, direction },
      {
        onError: () => {
          moved.current = null;
        },
      },
    );
  }

  function handlePrimary(image: AdminImage) {
    resetActions();
    setPrimary.mutate(image.id);
  }

  function handleConfirmDelete() {
    if (!deleting) {
      return;
    }
    resetActions();
    remove.mutate(deleting.id, {
      onSettled: () => {
        setDeleting(null);
        // The row with the button is gone, so the focus goes back to the heading of the section.
        heading.current?.focus();
      },
    });
  }

  return (
    <section className="section" aria-labelledby="images-title">
      <div className="page-header">
        <h3 id="images-title" ref={heading} tabIndex={-1}>
          {t('images.title')}
        </h3>
      </div>

      {actionError ? (
        <p role="alert" className="form-error">
          {t(messageForActionError(actionError))}
        </p>
      ) : null}

      {images.length === 0 ? (
        <p>{t('images.empty')}</p>
      ) : (
        <ul className="variant-list" aria-label={t('images.list')}>
          {images.map((image, position) => (
            <li key={image.id}>
              <ImageRow
                image={image}
                product={product}
                imageBaseUrl={imageBaseUrl}
                position={position}
                count={images.length}
                busy={busy}
                onMove={handleMove}
                onPrimary={handlePrimary}
                onEdit={(chosen) => {
                  setEditingId(chosen.id);
                }}
                onDelete={setDeleting}
              />
            </li>
          ))}
        </ul>
      )}

      {editedImage ? (
        <ImageEditForm
          // A different image starts the form again with its own values.
          key={editedImage.id}
          product={product}
          image={editedImage}
          onDone={() => {
            setEditingId(null);
            heading.current?.focus();
          }}
        />
      ) : null}

      <ImageUploadForm product={product} full={images.length >= IMAGE_MAX_COUNT} />

      <ConfirmDialog
        open={deleting !== null}
        title={t('images.deleteTitle')}
        confirmLabel={t('images.deleteConfirm')}
        cancelLabel={t('images.deleteCancel')}
        busy={remove.isPending}
        onConfirm={handleConfirmDelete}
        onCancel={() => {
          setDeleting(null);
        }}
      >
        <p>{t('images.deleteMessage', { alt: deleting?.altText ?? '' })}</p>
        {deleting?.isPrimary && images.length > 1 ? <p>{t('images.deletePrimaryNote')}</p> : null}
      </ConfirmDialog>
    </section>
  );
}
