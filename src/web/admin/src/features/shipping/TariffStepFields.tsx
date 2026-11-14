import { FormField } from '../../components/FormField';
import { t, type MessageKey } from '../../i18n';
import type { StepErrors, StepValues } from './shippingValidation';

function message(key: MessageKey | undefined): string | undefined {
  return key === undefined ? undefined : t(key);
}

interface TariffStepFieldsProps {
  row: StepValues;
  /** The place of the step in the list, from 1. */
  number: number;
  currency: string;
  errors: StepErrors | undefined;
  /** True for a step that was just added: its weight gets the focus. */
  hasFocus: boolean;
  onChange: (field: 'weight' | 'price', value: string) => void;
  onRemove: () => void;
}

/** One step of the tariff: the heaviest parcel it covers, its price, and a button that removes it. */
export function TariffStepFields({
  row,
  number,
  currency,
  errors,
  hasFocus,
  onChange,
  onRemove,
}: TariffStepFieldsProps) {
  const key = String(row.key);
  return (
    <fieldset className="tariff-step">
      <legend>{t('shipping.step', { number: String(number) })}</legend>
      <FormField
        id={`shipping-weight-${key}`}
        name={`weight-${key}`}
        label={t('shipping.weight')}
        error={message(errors?.weight)}
      >
        {(control) => (
          <input
            {...control}
            type="text"
            inputMode="numeric"
            autoComplete="off"
            autoFocus={hasFocus}
            value={row.weight}
            onChange={(event) => {
              onChange('weight', event.target.value);
            }}
          />
        )}
      </FormField>
      <FormField
        id={`shipping-price-${key}`}
        name={`price-${key}`}
        label={t('shipping.price', { currency })}
        error={message(errors?.price)}
      >
        {(control) => (
          <input
            {...control}
            type="text"
            inputMode="decimal"
            autoComplete="off"
            value={row.price}
            onChange={(event) => {
              onChange('price', event.target.value);
            }}
          />
        )}
      </FormField>
      <button type="button" onClick={onRemove}>
        {t('shipping.remove', { number: String(number) })}
      </button>
    </fieldset>
  );
}
