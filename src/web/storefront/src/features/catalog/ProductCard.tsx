import { formatMoney, type ProductListItem } from '@storefront/shared';
import { useState } from 'react';
import { Link } from 'react-router';
import { ImagePlaceholder } from '../product/ImagePlaceholder';
import { useStoreSettings, useT } from '../store-settings/useStoreSettings';
import { describeAvailability } from './availability';

const IMAGE_SIZE = 320;

/**
 * The main image of the product, or the placeholder when there is none, the environment has no image address,
 * or the file does not load. The box keeps its size in every case, so the cards do not jump while images load.
 * The link is only for the mouse: the name of the product below is the link for the keyboard and the screen reader.
 */
function CardImage({ product }: { product: ProductListItem }) {
  const [failed, setFailed] = useState(false);
  const image = product.primaryImage;
  const showImage = image !== null && image.url !== null && !failed;

  return (
    <Link to={`/products/${product.slug}`} tabIndex={-1} className="product-card-image">
      {showImage ? (
        <img
          src={image.url ?? undefined}
          alt={image.altText}
          width={IMAGE_SIZE}
          height={IMAGE_SIZE}
          loading="lazy"
          onError={() => {
            setFailed(true);
          }}
        />
      ) : (
        <ImagePlaceholder />
      )}
    </Link>
  );
}

export function ProductCard({ product }: { product: ProductListItem }) {
  const t = useT();
  const { defaultCurrency, defaultLocale } = useStoreSettings();

  return (
    <li className="product-card">
      <CardImage product={product} />
      <h2>
        <Link to={`/products/${product.slug}`}>{product.name}</Link>
      </h2>
      <p>{product.shortDescription}</p>
      <p className="product-price">
        {t('catalog.priceFrom')} {formatMoney(product.minPrice, defaultCurrency, defaultLocale)}
      </p>
      <p>{describeAvailability(t, defaultLocale, product)}</p>
    </li>
  );
}
