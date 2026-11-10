import { ApiError, type AdminCategory } from '@storefront/shared';
import { useState } from 'react';
import { Link } from 'react-router';
import { t, type MessageKey } from '../../i18n';
import { buildCategoryTree, siblingsOf } from './categoryTree';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { useAdminCategories, useMoveCategory, useSetCategoryActive } from './useCategories';

/** A 409 means someone else saved the category first; the list is reloaded and the action can be repeated. */
function messageForActionError(error: Error): MessageKey {
  return error instanceof ApiError && error.status === 409
    ? 'categories.error.changedByOther'
    : 'categories.error.actionFailed';
}

interface RowProps {
  category: AdminCategory;
  all: readonly AdminCategory[];
  busy: boolean;
  onMove: (category: AdminCategory, direction: 'Up' | 'Down') => void;
  onActivate: (category: AdminCategory) => void;
  onDeactivate: (category: AdminCategory) => void;
}

function CategoryRow({ category, all, busy, onMove, onActivate, onDeactivate }: RowProps) {
  const siblings = siblingsOf(all, category);
  const position = siblings.findIndex((sibling) => sibling.id === category.id);
  const name = { name: category.name };

  return (
    <div className={category.isActive ? 'category-row' : 'category-row inactive'}>
      <div className="category-info">
        <strong>{category.name}</strong>
        {!category.isActive ? <span className="badge">{t('categories.inactive')}</span> : null}
        <span className="category-meta">
          {category.slug} · {t('categories.products', { count: category.productCount })}
        </span>
      </div>
      <div className="category-actions">
        <button
          type="button"
          className="secondary"
          aria-label={t('categories.moveUp', name)}
          disabled={busy || position <= 0}
          onClick={() => {
            onMove(category, 'Up');
          }}
        >
          ↑
        </button>
        <button
          type="button"
          className="secondary"
          aria-label={t('categories.moveDown', name)}
          disabled={busy || position < 0 || position >= siblings.length - 1}
          onClick={() => {
            onMove(category, 'Down');
          }}
        >
          ↓
        </button>
        <Link
          className="button secondary"
          to={`/categories/${category.id}`}
          aria-label={t('categories.edit', name)}
        >
          {t('categories.editShort')}
        </Link>
        {category.isActive ? (
          <button
            type="button"
            className="secondary"
            aria-label={t('categories.deactivate', name)}
            disabled={busy}
            onClick={() => {
              onDeactivate(category);
            }}
          >
            {t('categories.deactivateShort')}
          </button>
        ) : (
          <button
            type="button"
            className="secondary"
            aria-label={t('categories.activate', name)}
            disabled={busy}
            onClick={() => {
              onActivate(category);
            }}
          >
            {t('categories.activateShort')}
          </button>
        )}
      </div>
    </div>
  );
}

export function CategoryListPage() {
  const categories = useAdminCategories();
  const move = useMoveCategory();
  const setActive = useSetCategoryActive();
  const [toDeactivate, setToDeactivate] = useState<AdminCategory | null>(null);

  if (categories.isPending) {
    return <p role="status">{t('categories.loading')}</p>;
  }
  if (categories.isError) {
    return (
      <>
        <p role="alert" className="form-error">
          {t('categories.error.loadFailed')}
        </p>
        <button
          type="button"
          onClick={() => {
            void categories.refetch();
          }}
        >
          {t('auth.retry')}
        </button>
      </>
    );
  }

  const all = categories.data;
  const tree = buildCategoryTree(all);
  const busy = move.isPending || setActive.isPending;
  const actionError = move.error ?? (toDeactivate ? null : setActive.error);

  function handleMove(category: AdminCategory, direction: 'Up' | 'Down') {
    setActive.reset();
    move.mutate({ id: category.id, direction, version: category.version });
  }

  function handleActivate(category: AdminCategory) {
    move.reset();
    setActive.mutate({ id: category.id, isActive: true, version: category.version });
  }

  function handleDeactivate(category: AdminCategory) {
    move.reset();
    setActive.reset();
    setToDeactivate(category);
  }

  function confirmDeactivate() {
    if (!toDeactivate) {
      return;
    }
    setActive.mutate(
      { id: toDeactivate.id, isActive: false, version: toDeactivate.version },
      {
        onSettled: () => {
          setToDeactivate(null);
        },
      },
    );
  }

  const children = toDeactivate ? all.filter((item) => item.parentId === toDeactivate.id) : [];
  const affectedProducts = toDeactivate
    ? children.reduce((sum, child) => sum + child.productCount, toDeactivate.productCount)
    : 0;

  return (
    <>
      <div className="page-header">
        <h2>{t('categories.title')}</h2>
        <Link className="button" to="/categories/new">
          {t('categories.new')}
        </Link>
      </div>

      {actionError ? (
        <p role="alert" className="form-error">
          {t(messageForActionError(actionError))}
        </p>
      ) : null}

      {tree.length === 0 ? (
        <p>{t('categories.empty')}</p>
      ) : (
        <ul className="category-tree" aria-label={t('categories.title')}>
          {tree.map((node) => (
            <li key={node.category.id}>
              <CategoryRow
                category={node.category}
                all={all}
                busy={busy}
                onMove={handleMove}
                onActivate={handleActivate}
                onDeactivate={handleDeactivate}
              />
              {node.children.length > 0 ? (
                <ul aria-label={t('categories.subcategoriesOf', { name: node.category.name })}>
                  {node.children.map((child) => (
                    <li key={child.id}>
                      <CategoryRow
                        category={child}
                        all={all}
                        busy={busy}
                        onMove={handleMove}
                        onActivate={handleActivate}
                        onDeactivate={handleDeactivate}
                      />
                    </li>
                  ))}
                </ul>
              ) : null}
            </li>
          ))}
        </ul>
      )}

      <ConfirmDialog
        open={toDeactivate !== null}
        title={t('categories.deactivateDialog.title', { name: toDeactivate?.name ?? '' })}
        confirmLabel={t('categories.deactivateDialog.confirm')}
        cancelLabel={t('categories.deactivateDialog.cancel')}
        busy={setActive.isPending}
        onConfirm={confirmDeactivate}
        onCancel={() => {
          setToDeactivate(null);
        }}
      >
        <p>{t('categories.deactivateDialog.effect', { name: toDeactivate?.name ?? '' })}</p>
        <p>
          {t('categories.deactivateDialog.counts', {
            subcategories: children.length,
            products: affectedProducts,
          })}
        </p>
        <p>{t('categories.deactivateDialog.reversible')}</p>
      </ConfirmDialog>
    </>
  );
}
