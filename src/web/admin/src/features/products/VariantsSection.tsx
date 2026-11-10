import {
  ApiError,
  formatMoney,
  type AdminProduct,
  type AdminVariant,
  type StoreMoneySettings,
} from '@storefront/shared';
import { useRef, useState } from 'react';
import { t, type MessageKey } from '../../i18n';
import { VariantForm } from './VariantForm';
import { useMoveVariant, useSetVariantActive } from './useProducts';

function messageForActionError(error: Error): MessageKey {
  return error instanceof ApiError && error.status === 409 && error.code === 'variant.last_active'
    ? 'variants.error.lastActive'
    : 'variants.error.actionFailed';
}

/** Which variant is open in the form: a new one, an existing one, or none. */
type Editing = { kind: 'none' } | { kind: 'new' } | { kind: 'edit'; variantId: string };

interface RowProps {
  variant: AdminVariant;
  product: AdminProduct;
  money: StoreMoneySettings;
  position: number;
  count: number;
  busy: boolean;
  onMove: (variant: AdminVariant, direction: 'Up' | 'Down') => void;
  onToggle: (variant: AdminVariant) => void;
  onEdit: (variant: AdminVariant) => void;
}

function VariantRow({
  variant,
  product,
  money,
  position,
  count,
  busy,
  onMove,
  onToggle,
  onEdit,
}: RowProps) {
  const name = { name: variant.name };
  const price = (minorUnits: number) => formatMoney(minorUnits, money.currency, money.locale);

  return (
    <div className={variant.isActive ? 'category-row' : 'category-row inactive'}>
      <div className="category-info">
        <strong>{variant.name}</strong>
        {!variant.isActive ? <span className="badge">{t('variants.inactive')}</span> : null}
        <span className="category-meta">
          {variant.sku} · {price(variant.price)}
          {variant.compareAtPrice !== null
            ? ` · ${t('variants.compareAt', { price: price(variant.compareAtPrice) })}`
            : ''}
          {' · '}
          {product.fulfilmentType === 'InStock' && variant.stockQuantity !== null
            ? t('variants.stock', { count: variant.stockQuantity })
            : t('variants.madeToOrder')}
        </span>
      </div>
      <div className="category-actions">
        <button
          type="button"
          className="secondary"
          aria-label={t('variants.moveUp', name)}
          disabled={busy || position <= 0}
          onClick={() => {
            onMove(variant, 'Up');
          }}
        >
          ↑
        </button>
        <button
          type="button"
          className="secondary"
          aria-label={t('variants.moveDown', name)}
          disabled={busy || position >= count - 1}
          onClick={() => {
            onMove(variant, 'Down');
          }}
        >
          ↓
        </button>
        <button
          type="button"
          className="secondary"
          aria-label={t('variants.edit', name)}
          disabled={busy}
          onClick={() => {
            onEdit(variant);
          }}
        >
          {t('variants.editShort')}
        </button>
        <button
          type="button"
          className="secondary"
          aria-label={
            variant.isActive ? t('variants.deactivate', name) : t('variants.activate', name)
          }
          disabled={busy}
          onClick={() => {
            onToggle(variant);
          }}
        >
          {variant.isActive ? t('variants.deactivateShort') : t('variants.activateShort')}
        </button>
      </div>
    </div>
  );
}

export interface VariantsSectionProps {
  product: AdminProduct;
  money: StoreMoneySettings;
}

/** The variants of a product: list, add, edit, activate and deactivate, and the order. */
export function VariantsSection({ product, money }: VariantsSectionProps) {
  const move = useMoveVariant(product.id);
  const setActive = useSetVariantActive(product.id);
  const [editing, setEditing] = useState<Editing>({ kind: 'none' });
  const heading = useRef<HTMLHeadingElement>(null);

  const busy = move.isPending || setActive.isPending;
  const actionError = move.error ?? setActive.error;
  const variants = product.variants;
  const nextSortOrder = Math.max(-1, ...variants.map((variant) => variant.sortOrder)) + 1;
  const editedVariant =
    editing.kind === 'edit'
      ? variants.find((variant) => variant.id === editing.variantId)
      : undefined;

  function close() {
    setEditing({ kind: 'none' });
    // The form is gone, so the focus goes back to the heading of the section instead of getting lost.
    heading.current?.focus();
  }

  function handleMove(variant: AdminVariant, direction: 'Up' | 'Down') {
    setActive.reset();
    move.mutate({ variantId: variant.id, direction });
  }

  function handleToggle(variant: AdminVariant) {
    move.reset();
    setActive.mutate({ variantId: variant.id, isActive: !variant.isActive });
  }

  return (
    <section className="section" aria-labelledby="variants-title">
      <div className="page-header">
        <h3 id="variants-title" ref={heading} tabIndex={-1}>
          {t('variants.title')}
        </h3>
        <button
          type="button"
          disabled={editing.kind === 'new'}
          onClick={() => {
            setEditing({ kind: 'new' });
          }}
        >
          {t('variants.add')}
        </button>
      </div>

      {actionError ? (
        <p role="alert" className="form-error">
          {t(messageForActionError(actionError))}
        </p>
      ) : null}

      {variants.length === 0 ? (
        <p>{t('variants.empty')}</p>
      ) : (
        <ul className="variant-list" aria-label={t('variants.list')}>
          {variants.map((variant, position) => (
            <li key={variant.id}>
              <VariantRow
                variant={variant}
                product={product}
                money={money}
                position={position}
                count={variants.length}
                busy={busy}
                onMove={handleMove}
                onToggle={handleToggle}
                onEdit={(chosen) => {
                  setEditing({ kind: 'edit', variantId: chosen.id });
                }}
              />
            </li>
          ))}
        </ul>
      )}

      {editing.kind === 'new' || editedVariant ? (
        <VariantForm
          // A different variant starts the form again with its own values.
          key={editedVariant?.id ?? 'new'}
          product={product}
          variant={editedVariant}
          money={money}
          nextSortOrder={nextSortOrder}
          onDone={close}
        />
      ) : null}
    </section>
  );
}
