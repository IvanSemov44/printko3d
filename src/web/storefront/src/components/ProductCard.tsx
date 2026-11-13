import { formatMoney, type ProductListItem } from '@storefront/shared';
import { useState } from 'react';
import { Link } from 'react-router';
import { ImagePlaceholder } from './ImagePlaceholder';
import { useStoreSettings, useT } from '../features/store-settings/useStoreSettings';
import { availabilityKind, describeAvailability } from './availability';

const IMAGE_SIZE = 320;

/**
 * The main image of the product, or the placeholder when there is none, the environment has no image address,
 * or the file does not load. The box keeps its size in every case, so the cards do not jump while images load.
 */
function CardImage({ product }: { product: ProductListItem }) {
  const [failed, setFailed] = useState(false);
  const image = product.primaryImage;
  const showImage = image !== null && image.url !== null && !failed;

  return (
    <span className="product-card-image">
      {showImage ? (
        <img
          src={image.url ?? undefined}
          alt={image.altText !== '' ? image.altText : product.name}
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
    </span>
  );
}

/** The whole card is one link (no nested links). Its name is all its visible text (WCAG 2.5.3). */
export function ProductCard({ product }: { product: ProductListItem }) {
  const t = useT();
  const { defaultCurrency, defaultLocale } = useStoreSettings();
  const kind = availabilityKind(product);

  return (
    <li className="product-card">
      <Link to={`/products/${product.slug}`} className="product-card-link">
        <span className="product-card-media">
          <CardImage product={product} />
          <span className={`badge badge-${kind}`}>
            {describeAvailability(t, defaultLocale, product)}
          </span>
        </span>
        <span className="product-card-body">
          <span className="product-card-title">{product.name}</span>
          {product.shortDescription !== '' && (
            <span className="product-card-text">{product.shortDescription}</span>
          )}
          <span className="product-price">
            {t('catalog.priceFrom')} {formatMoney(product.minPrice, defaultCurrency, defaultLocale)}
          </span>
        </span>
      </Link>
    </li>
  );
}
