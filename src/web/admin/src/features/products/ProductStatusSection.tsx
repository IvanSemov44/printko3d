import { ApiError, type AdminProduct, type ProductStatus } from '@storefront/shared';
import { useState } from 'react';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { t, type MessageKey } from '../../i18n';
import { STATUS_HINT_KEYS, STATUS_KEYS } from './productLabels';
import { useSetProductStatus } from './useProducts';

/** The states a product can go to from each state, with the text of the button. */
const MOVES: Record<ProductStatus, readonly { to: ProductStatus; label: MessageKey }[]> = {
  Draft: [
    { to: 'Published', label: 'products.statusSection.publish' },
    { to: 'Archived', label: 'products.statusSection.archive' },
  ],
  Published: [
    { to: 'Draft', label: 'products.statusSection.toDraft' },
    { to: 'Archived', label: 'products.statusSection.archive' },
  ],
  Archived: [
    { to: 'Published', label: 'products.statusSection.publish' },
    { to: 'Draft', label: 'products.statusSection.toDraft' },
  ],
};

function messageForError(error: Error): MessageKey {
  if (error instanceof ApiError && error.status === 409) {
    return error.code === 'product.cannot_publish_without_active_variant'
      ? 'products.error.cannotPublish'
      : 'products.error.versionConflict';
  }
  return 'products.error.statusFailed';
}

export interface ProductStatusSectionProps {
  product: AdminProduct;
  /** Loads the product again after someone else changed it. */
  onReload: () => void;
}

/** Publish, archive and return to draft. A change that hides a published product from the shop asks first. */
export function ProductStatusSection({ product, onReload }: ProductStatusSectionProps) {
  const setStatus = useSetProductStatus();
  const [toConfirm, setToConfirm] = useState<ProductStatus | null>(null);

  function change(status: ProductStatus) {
    setStatus.mutate(
      { id: product.id, status, version: product.version },
      {
        onSettled: () => {
          setToConfirm(null);
        },
      },
    );
  }

  function handleClick(status: ProductStatus) {
    setStatus.reset();
    // Only what hides a visible product needs a confirmation; publishing and quiet moves do not.
    if (product.status === 'Published') {
      setToConfirm(status);
    } else {
      change(status);
    }
  }

  const error = setStatus.error;
  const isVersionConflict =
    error !== null && messageForError(error) === 'products.error.versionConflict';
  const archiving = toConfirm === 'Archived';

  return (
    <section className="section" aria-labelledby="product-status-title">
      <h3 id="product-status-title">{t('products.statusSection.title')}</h3>
      <p>
        {t('products.statusSection.current', { status: t(STATUS_KEYS[product.status]) })}
        {' · '}
        {t(STATUS_HINT_KEYS[product.status])}
      </p>

      {error ? (
        <div role="alert" className="form-error">
          <p>{t(messageForError(error))}</p>
          {isVersionConflict ? (
            <button type="button" onClick={onReload}>
              {t('products.reload')}
            </button>
          ) : null}
        </div>
      ) : null}

      <div className="form-actions">
        {MOVES[product.status].map((move) => (
          <button
            key={move.to}
            type="button"
            className={move.to === 'Published' ? undefined : 'secondary'}
            disabled={setStatus.isPending}
            onClick={() => {
              handleClick(move.to);
            }}
          >
            {setStatus.isPending && setStatus.variables.status === move.to
              ? t('products.statusSection.working')
              : t(move.label)}
          </button>
        ))}
      </div>

      <ConfirmDialog
        open={toConfirm !== null}
        title={
          archiving
            ? t('products.statusDialog.archiveTitle', { name: product.name })
            : t('products.statusDialog.draftTitle', { name: product.name })
        }
        confirmLabel={
          archiving
            ? t('products.statusDialog.archiveConfirm')
            : t('products.statusDialog.draftConfirm')
        }
        cancelLabel={t('products.statusDialog.cancel')}
        busy={setStatus.isPending}
        onConfirm={() => {
          if (toConfirm) {
            change(toConfirm);
          }
        }}
        onCancel={() => {
          setToConfirm(null);
        }}
      >
        <p>
          {archiving
            ? t('products.statusDialog.archiveEffect')
            : t('products.statusDialog.draftEffect')}
        </p>
        <p>{t('products.statusDialog.reversible')}</p>
      </ConfirmDialog>
    </section>
  );
}
