import type { ProductDetails } from '@storefront/shared';
import { PageMeta } from '../seo/PageMeta';
import { ProductStructuredData } from '../seo/ProductStructuredData';
import { ProductGallery } from './ProductGallery';
import { ProductOffer } from './ProductOffer';
import { useSelectedVariant } from './useSelectedVariant';
import { VariantPicker } from './VariantPicker';
import { defaultImage, imagesForVariant } from './variantSelection';

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
      <h1>{product.name}</h1>
      {product.shortDescription !== '' && (
        <p className="product-short">{product.shortDescription}</p>
      )}
      <div className="product-layout">
        <ProductGallery
          images={imagesForVariant(product.images, variant?.id)}
          fallbackAlt={product.name}
        />
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
