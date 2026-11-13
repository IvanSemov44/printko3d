import { FormField } from '../../components/FormField';
import { useT } from '../store-settings/useStoreSettings';
import type { CheckoutErrors, CheckoutValues } from './checkoutValidation';

interface CheckoutFieldsProps {
  values: CheckoutValues;
  errors: CheckoutErrors;
  onChange: (field: keyof CheckoutValues, value: string) => void;
}

interface FieldSpec {
  field: keyof CheckoutValues;
  id: string;
  label: Parameters<ReturnType<typeof useT>>[0];
  type: 'text' | 'email' | 'tel';
  autoComplete: string;
  inputMode?: 'numeric';
}

const CONTACT: readonly FieldSpec[] = [
  {
    field: 'name',
    id: 'checkout-name',
    label: 'checkout.name',
    type: 'text',
    autoComplete: 'name',
  },
  {
    field: 'email',
    id: 'checkout-email',
    label: 'checkout.email',
    type: 'email',
    autoComplete: 'email',
  },
  {
    field: 'phone',
    id: 'checkout-phone',
    label: 'checkout.phone',
    type: 'tel',
    autoComplete: 'tel',
  },
];

const ADDRESS: readonly FieldSpec[] = [
  {
    field: 'street',
    id: 'checkout-street',
    label: 'checkout.street',
    type: 'text',
    autoComplete: 'street-address',
  },
  {
    field: 'city',
    id: 'checkout-city',
    label: 'checkout.city',
    type: 'text',
    autoComplete: 'address-level2',
  },
  {
    field: 'postalCode',
    id: 'checkout-postal-code',
    label: 'checkout.postalCode',
    type: 'text',
    autoComplete: 'postal-code',
    inputMode: 'numeric',
  },
];

/** Contact and address of the buyer. The country is always Bulgaria for now, so it is not asked. */
export function CheckoutFields({ values, errors, onChange }: CheckoutFieldsProps) {
  const t = useT();

  const render = (specs: readonly FieldSpec[]) =>
    specs.map((spec) => {
      const errorKey = errors[spec.field];
      return (
        <FormField
          key={spec.field}
          id={spec.id}
          name={spec.field}
          label={t(spec.label)}
          error={errorKey === undefined ? undefined : t(errorKey)}
        >
          {(control) => (
            <input
              {...control}
              type={spec.type}
              inputMode={spec.inputMode}
              autoComplete={spec.autoComplete}
              value={values[spec.field]}
              onChange={(event) => {
                onChange(spec.field, event.target.value);
              }}
            />
          )}
        </FormField>
      );
    });

  return (
    <div className="checkout-fields">
      <fieldset>
        <legend>{t('checkout.contact')}</legend>
        {render(CONTACT)}
      </fieldset>
      <fieldset>
        <legend>{t('checkout.address')}</legend>
        {render(ADDRESS)}
      </fieldset>
    </div>
  );
}
