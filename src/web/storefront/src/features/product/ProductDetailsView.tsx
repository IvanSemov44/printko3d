import type { ProductDetails } from '@storefront/shared';
import { ProductGallery } from './ProductGallery';
import { ProductOffer } from './ProductOffer';
import { useSelectedVariant } from './useSelectedVariant';
import { VariantPicker } from './VariantPicker';
import { imagesForVariant } from './variantSelection';

/** A loaded product. Descriptions are plain text: React escapes them, nothing is inserted as HTML. */
export function ProductDetailsView({ product }: { product: ProductDetails }) {
  const { variant, select } = useSelectedVariant(product.variants);

  return (
    <article className="product-page">
      <h1>{product.name}</h1>
      {product.shortDescription !== '' && (
        <p className="product-short">{product.shortDescription}</p>
      )}
      <div className="product-layout">
        <ProductGallery images={imagesForVariant(product.images, variant?.id)} />
        <div className="product-info">
          {variant && (
            <>
              <VariantPicker variants={product.variants} selected={variant} onSelect={select} />
              <ProductOffer product={product} variant={variant} />
            </>
          )}
          {product.description !== '' && (
            <p className="product-description">{product.description}</p>
          )}
        </div>
      </div>
    </article>
  );
}
