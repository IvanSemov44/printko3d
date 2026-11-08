import type { ProductImage } from '@storefront/shared';
import { useState } from 'react';
import { useT } from '../store-settings/useStoreSettings';
import { ImagePlaceholder } from './ImagePlaceholder';
import { StoredImage } from './StoredImage';
import { defaultImage } from './variantSelection';

const LARGE_SIZE = 800;
const THUMBNAIL_SIZE = 96;

/** The large image plus, when there are several, thumbnails as toggle buttons (keyboard and screen reader friendly). */
export function ProductGallery({ images }: { images: ProductImage[] }) {
  const t = useT();
  const [chosenId, setChosenId] = useState<string | null>(null);
  const [failedIds, setFailedIds] = useState<ReadonlySet<string>>(new Set());

  // A chosen image that is no longer shown (another variant was selected) falls back to the default.
  const current = images.find((image) => image.id === chosenId) ?? defaultImage(images);
  const markFailed = (id: string) => {
    setFailedIds((previous) => new Set(previous).add(id));
  };

  return (
    <section className="gallery" aria-label={t('product.gallery')}>
      <div className="gallery-main">
        {current ? (
          <StoredImage
            image={current}
            alt={current.altText}
            size={LARGE_SIZE}
            isLarge
            failed={failedIds.has(current.id)}
            onError={() => {
              markFailed(current.id);
            }}
          />
        ) : (
          <ImagePlaceholder />
        )}
      </div>
      {images.length > 1 && (
        <ul className="gallery-thumbs">
          {images.map((image, index) => (
            <li key={image.id}>
              <button
                type="button"
                className="gallery-thumb"
                aria-pressed={image.id === current?.id}
                aria-label={
                  image.altText !== ''
                    ? image.altText
                    : t('product.imageNumber').replace('{number}', String(index + 1))
                }
                onClick={() => {
                  setChosenId(image.id);
                }}
              >
                <StoredImage
                  image={image}
                  alt=""
                  size={THUMBNAIL_SIZE}
                  isLarge={false}
                  failed={failedIds.has(image.id)}
                  onError={() => {
                    markFailed(image.id);
                  }}
                />
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
