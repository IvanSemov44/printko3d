import {
  ApiError,
  currencyFractionDigits,
  formatMoneyInput,
  type ShippingTariffStep,
} from '@storefront/shared';
import { useRef, useState, type SubmitEvent } from 'react';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { focusField } from '../../components/useFormValues';
import { t, type MessageKey } from '../../i18n';
import { useStoreMoney } from '../products/useProducts';
import { MAX_STEPS, validateTariff, type StepErrors, type StepValues } from './shippingValidation';
import { TariffStepFields } from './TariffStepFields';
import { useSaveShippingTariff, useShippingTariff } from './useShippingTariff';

interface FormProps {
  steps: readonly ShippingTariffStep[];
  currency: string;
  fractionDigits: number;
  onReload: () => void;
}

function TariffForm({ steps, currency, fractionDigits, onReload }: FormProps) {
  const save = useSaveShippingTariff();
  const nextKey = useRef(steps.length);
  const addButton = useRef<HTMLButtonElement>(null);
  const [rows, setRows] = useState<StepValues[]>(() =>
    steps.map((step, index) => ({
      key: index,
      weight: String(step.maxWeightGrams),
      price: formatMoneyInput(step.price, fractionDigits),
    })),
  );
  const [errors, setErrors] = useState<Record<number, StepErrors>>({});
  const [formError, setFormError] = useState<MessageKey | null>(null);
  const [conflict, setConflict] = useState(false);
  const [saved, setSaved] = useState(false);
  const [newKey, setNewKey] = useState<number | null>(null);
  const [asking, setAsking] = useState<HTMLFormElement | null>(null);

  function change(key: number, field: 'weight' | 'price', value: string) {
    setSaved(false);
    setRows((current) =>
      current.map((row) => (row.key === key ? { ...row, [field]: value } : row)),
    );
  }

  function add() {
    const key = nextKey.current++;
    setSaved(false);
    setNewKey(key);
    setRows((current) => [...current, { key, weight: '', price: '' }]);
  }

  function remove(key: number) {
    setSaved(false);
    setRows((current) => current.filter((row) => row.key !== key));
    setErrors((current) =>
      Object.fromEntries(Object.entries(current).filter(([k]) => Number(k) !== key)),
    );
    // The row that had the focus is gone; the button that adds one is where the focus can stay.
    addButton.current?.focus();
  }

  function send(sent: readonly ShippingTariffStep[]) {
    save.mutate(sent, {
      onSuccess: (result) => {
        setSaved(true);
        nextKey.current = result.length;
        setRows(
          result.map((step, index) => ({
            key: index,
            weight: String(step.maxWeightGrams),
            price: formatMoneyInput(step.price, fractionDigits),
          })),
        );
      },
      onError: (error: Error) => {
        if (error instanceof ApiError && error.code === 'shipping.tariff_conflict') {
          setConflict(true);
        } else if (error instanceof ApiError && error.status === 400) {
          setFormError('shipping.error.rejected');
        } else {
          setFormError('shipping.error.saveFailed');
        }
      },
    });
  }

  function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    setFormError(null);
    setConflict(false);
    setSaved(false);

    const check = validateTariff(rows, fractionDigits);
    setErrors(check.errors);
    if (check.steps === null) {
      const firstWrong = rows.find((row) => check.errors[row.key] !== undefined);
      if (firstWrong !== undefined) {
        focusField(
          form,
          check.errors[firstWrong.key]?.weight
            ? `weight-${String(firstWrong.key)}`
            : `price-${String(firstWrong.key)}`,
        );
      }
      return;
    }
    // No step switches the delivery off, so the buyers cannot order: that is asked first.
    if (check.steps.length === 0) {
      setAsking(form);
      return;
    }
    send(check.steps);
  }

  return (
    <form onSubmit={submit} noValidate aria-labelledby="shipping-title">
      <h2 id="shipping-title">{t('shipping.title')}</h2>
      <p>{t('shipping.intro')}</p>

      {conflict ? (
        <div role="alert" className="form-error">
          <p>{t('shipping.error.conflict')}</p>
          <button type="button" onClick={onReload}>
            {t('shipping.reload')}
          </button>
        </div>
      ) : null}
      {formError ? (
        <p role="alert" className="form-error">
          {t(formError)}
        </p>
      ) : null}

      {rows.length === 0 ? <p className="field-hint">{t('shipping.empty')}</p> : null}
      {rows.map((row, index) => (
        <TariffStepFields
          key={row.key}
          row={row}
          number={index + 1}
          currency={currency}
          errors={errors[row.key]}
          hasFocus={row.key === newKey}
          onChange={(field, value) => {
            change(row.key, field, value);
          }}
          onRemove={() => {
            remove(row.key);
          }}
        />
      ))}

      <div className="form-actions">
        <button ref={addButton} type="button" onClick={add} disabled={rows.length >= MAX_STEPS}>
          {t('shipping.add')}
        </button>
        <button type="submit" disabled={save.isPending}>
          {save.isPending ? t('shipping.saving') : t('shipping.save')}
        </button>
      </div>
      <p role="status">{saved ? t('shipping.saved') : ''}</p>

      <ConfirmDialog
        open={asking !== null}
        title={t('shipping.offDialog.title')}
        confirmLabel={t('shipping.offDialog.confirm')}
        cancelLabel={t('shipping.offDialog.cancel')}
        onConfirm={() => {
          setAsking(null);
          send([]);
        }}
        onCancel={() => {
          setAsking(null);
        }}
      >
        <p>{t('shipping.offDialog.message')}</p>
      </ConfirmDialog>
    </form>
  );
}

/** The screen of the delivery tariff: loads the steps and the currency of the shop, then shows the form. */
export function ShippingTariffPage() {
  const tariff = useShippingTariff();
  const money = useStoreMoney();
  // A new key builds the form again from the steps that were just loaded (after a conflict).
  const [formKey, setFormKey] = useState(0);

  if (tariff.isPending || money.isPending) {
    return <p role="status">{t('shipping.loading')}</p>;
  }
  if (tariff.isError || money.isError) {
    return (
      <>
        <p role="alert" className="form-error">
          {t('shipping.error.loadFailed')}
        </p>
        <button
          type="button"
          onClick={() => {
            void tariff.refetch();
            void money.refetch();
          }}
        >
          {t('shipping.retry')}
        </button>
      </>
    );
  }

  return (
    <TariffForm
      key={formKey}
      steps={tariff.data}
      currency={money.data.currency}
      fractionDigits={currencyFractionDigits(money.data.currency, money.data.locale)}
      onReload={() => {
        void tariff.refetch().then((result) => {
          if (result.isSuccess) {
            setFormKey((key) => key + 1);
          }
        });
      }}
    />
  );
}
