import type { ProductImage } from '@storefront/shared';
import { ImagePlaceholder } from './ImagePlaceholder';

interface StoredImageProps {
  image: ProductImage;
  /** Text for screen readers; empty when the surrounding control already has a name. */
  alt: string;
  size: number;
  isLarge: boolean;
  failed: boolean;
  onError: () => void;
}

/** A product image with fixed dimensions; falls back to the placeholder when it has no address or fails to load. */
export function StoredImage({ image, alt, size, isLarge, failed, onError }: StoredImageProps) {
  if (image.url === null || failed) {
    return <ImagePlaceholder />;
  }
  return (
    <img
      src={image.url}
      alt={alt}
      width={size}
      height={size}
      loading={isLarge ? 'eager' : 'lazy'}
      onError={onError}
    />
  );
}
