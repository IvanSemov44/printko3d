import type { ProductDetails } from '@storefront/shared';
import { AddToCartButton } from '../cart/AddToCartButton';
import { availabilityKind } from '../../components/availability';
import { PageMeta } from '../seo/PageMeta';
import { ProductStructuredData } from '../seo/ProductStructuredData';
import { ProductGallery } from './ProductGallery';
import { ProductOffer } from './ProductOffer';
import { ProductSpecs } from './ProductSpecs';
import { useSelectedVariant } from './useSelectedVariant';
import { VariantPicker } from './VariantPicker';
import { defaultImage, imagesForVariant } from '../../components/variantSelection';

/** A loaded product. Descriptions are plain text: React escapes them, nothing is inserted as HTML. */
export function ProductDetailsView({ product }: { product: ProductDetails }) {
  const { variant, select } = useSelectedVariant(product.variants);

  return (
    <article className="product-page">
      <PageMeta
        pageTitle={product.seoTitle !== '' ? product.seoTitle : product.name}
        description={
          product.seoDescription !== '' ? product.seoDescription : product.shortDescription
        }
        image={defaultImage(product.images)?.url ?? null}
        type="product"
      />
      <ProductStructuredData product={product} />
      <div className="product-layout">
        <ProductGallery
          images={imagesForVariant(product.images, variant?.id)}
          fallbackAlt={product.name}
        />
        <div className="product-info">
          <h1>{product.name}</h1>
          {product.shortDescription !== '' && (
            <p className="product-short">{product.shortDescription}</p>
          )}
          {variant && (
            <>
              <ProductOffer product={product} variant={variant} />
              <VariantPicker variants={product.variants} selected={variant} onSelect={select} />
              <AddToCartButton
                variantId={variant.id}
                canBuy={
                  availabilityKind({
                    fulfilmentType: product.fulfilmentType,
                    isAvailable: variant.isAvailable,
                  }) !== 'soldOut'
                }
              />
            </>
          )}
          {product.description !== '' && (
            <p className="product-description">{product.description}</p>
          )}
          {variant && <ProductSpecs variant={variant} />}
        </div>
      </div>
    </article>
  );
}
