import {
  ApiError,
  MIN_PRIMARY_COLOR_CONTRAST,
  hasEnoughPrimaryColorContrast,
  isHexColor,
  primaryColorContrast,
  readableTextColor,
  type AdminStoreSettings,
  type StoreSettingsInput,
} from '@storefront/shared';
import { useState, type SubmitEvent } from 'react';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { FormField } from '../../components/FormField';
import { focusField, useFormValues } from '../../components/useFormValues';
import { t, type MessageKey } from '../../i18n';
import {
  CURRENCIES,
  LOCALES,
  NAME_MAX_LENGTH,
  SERVER_REJECTED_KEYS,
  SETTINGS_FIELD_ORDER,
  isSettingsField,
  validateSettings,
  type SettingsFormErrors,
} from './settingsValidation';
import { useAdminStoreSettings, useSaveStoreSettings } from './useStoreSettings';

/** The translated message of a field, or undefined when the field has no error. */
function message(key: MessageKey | undefined): string | undefined {
  return key === undefined ? undefined : t(key);
}

type Confirmation = 'currency' | 'demo';

/** The ratio rounded down to one decimal, so 2.997 is never shown as "3.0" next to the word "too low". */
function formatRatio(ratio: number, locale: string): string {
  return new Intl.NumberFormat(locale, {
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
  }).format(Math.floor(ratio * 10) / 10);
}

const CURRENCY_LABEL_KEYS: Partial<Record<string, MessageKey>> = {
  EUR: 'settings.form.currency.EUR',
  BGN: 'settings.form.currency.BGN',
};

function currencyLabel(code: string): string {
  const key = CURRENCY_LABEL_KEYS[code];
  return key === undefined ? code : t(key);
}

function localeLabel(code: string): string {
  return code === 'bg' ? t('settings.form.locale.bg') : code;
}

interface FormProps {
  settings: AdminStoreSettings;
  onReload: () => void;
}

function SettingsForm({ settings, onReload }: FormProps) {
  const save = useSaveStoreSettings();
  const { values, setValue } = useFormValues({
    storeName: settings.storeName,
    defaultCurrency: settings.defaultCurrency,
    defaultLocale: settings.defaultLocale,
    contactEmail: settings.contactEmail,
    contactPhone: settings.contactPhone,
    primaryColor: settings.primaryColor,
  });
  const [isDemo, setIsDemo] = useState(settings.isDemo);
  const [errors, setErrors] = useState<SettingsFormErrors>({});
  const [formError, setFormError] = useState<MessageKey | null>(null);
  const [conflict, setConflict] = useState(false);
  const [saved, setSaved] = useState(false);
  // The question that is on screen, and the ones already answered "yes" for this save.
  const [asking, setAsking] = useState<{
    kind: Confirmation;
    done: readonly Confirmation[];
  } | null>(null);
  const [formElement, setFormElement] = useState<HTMLFormElement | null>(null);

  // The currencies and languages of the choice include the current one, even if the list of the form does not.
  const currencies = [...new Set([settings.defaultCurrency, ...CURRENCIES])];
  const locales = [...new Set([settings.defaultLocale, ...LOCALES])];

  const colorIsHex = isHexColor(values.primaryColor);
  const contrast = colorIsHex ? primaryColorContrast(values.primaryColor) : null;
  const colorHint = [
    t('settings.form.primaryColorHint'),
    contrast === null
      ? null
      : t(
          hasEnoughPrimaryColorContrast(values.primaryColor)
            ? 'settings.color.contrastOk'
            : 'settings.color.contrastLow',
          {
            ratio: formatRatio(contrast, values.defaultLocale),
            min: MIN_PRIMARY_COLOR_CONTRAST,
          },
        ),
  ]
    .filter(Boolean)
    .join(' ');

  function required(): Confirmation[] {
    const list: Confirmation[] = [];
    if (values.defaultCurrency !== settings.defaultCurrency) {
      list.push('currency');
    }
    if (settings.isDemo && !isDemo) {
      list.push('demo');
    }
    return list;
  }

  function handleError(error: Error, form: HTMLFormElement) {
    if (error instanceof ApiError && error.status === 409) {
      if (error.code === 'store_settings.currency_locked') {
        setErrors({ defaultCurrency: 'settings.error.currencyLocked' });
        focusField(form, 'defaultCurrency');
      } else {
        setConflict(true);
      }
    } else if (error instanceof ApiError && error.status === 400) {
      const rejected = error.fields.filter(isSettingsField);
      const first = SETTINGS_FIELD_ORDER.find((field) => rejected.includes(field));
      if (first === undefined) {
        setFormError('settings.error.rejected');
      } else {
        setErrors(
          Object.fromEntries(rejected.map((field) => [field, SERVER_REJECTED_KEYS[field]])),
        );
        focusField(form, first);
      }
    } else {
      setFormError('settings.error.saveFailed');
    }
  }

  function send(form: HTMLFormElement) {
    const input: StoreSettingsInput = {
      storeName: values.storeName.trim(),
      defaultCurrency: values.defaultCurrency,
      defaultLocale: values.defaultLocale,
      contactEmail: values.contactEmail,
      contactPhone: values.contactPhone.trim(),
      primaryColor: values.primaryColor.toLowerCase(),
      isDemo,
    };
    save.mutate(
      { input, version: settings.version },
      {
        onSuccess: (result) => {
          setSaved(true);
          setValue('storeName', result.storeName);
          setValue('contactPhone', result.contactPhone);
          setValue('primaryColor', result.primaryColor);
        },
        onError: (error: Error) => {
          handleError(error, form);
        },
      },
    );
  }

  /** Asks the next question that has not been answered yet, or sends the form when there is none. */
  function continueSave(form: HTMLFormElement, done: readonly Confirmation[]) {
    const next = required().find((kind) => !done.includes(kind));
    if (next === undefined) {
      setAsking(null);
      send(form);
    } else {
      setAsking({ kind: next, done });
    }
  }

  function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    setFormElement(form);
    setFormError(null);
    setConflict(false);
    setSaved(false);

    const found = validateSettings(values);
    setErrors(found);
    const firstInvalid = SETTINGS_FIELD_ORDER.find((field) => found[field] !== undefined);
    if (firstInvalid) {
      focusField(form, firstInvalid);
      return;
    }
    continueSave(form, []);
  }

  function answerYes() {
    if (asking && formElement) {
      continueSave(formElement, [...asking.done, asking.kind]);
    }
  }

  return (
    <form onSubmit={handleSubmit} noValidate aria-labelledby="settings-title">
      <h2 id="settings-title">{t('settings.title')}</h2>

      {conflict ? (
        <div role="alert" className="form-error">
          <p>{t('settings.error.versionConflict')}</p>
          <button type="button" onClick={onReload}>
            {t('settings.reload')}
          </button>
        </div>
      ) : null}
      {formError ? (
        <p role="alert" className="form-error">
          {t(formError)}
        </p>
      ) : null}

      <FormField
        id="settings-store-name"
        name="storeName"
        label={t('settings.form.storeName')}
        error={message(errors.storeName)}
      >
        {(control) => (
          <input
            {...control}
            type="text"
            autoComplete="off"
            value={values.storeName}
            maxLength={NAME_MAX_LENGTH + 100}
            onChange={(event) => {
              setSaved(false);
              setValue('storeName', event.target.value);
            }}
          />
        )}
      </FormField>

      <FormField
        id="settings-currency"
        name="defaultCurrency"
        label={t('settings.form.currency')}
        hint={t('settings.form.currencyHint')}
        error={message(errors.defaultCurrency)}
      >
        {(control) => (
          <select
            {...control}
            value={values.defaultCurrency}
            onChange={(event) => {
              setSaved(false);
              setValue('defaultCurrency', event.target.value);
            }}
          >
            {currencies.map((code) => (
              <option key={code} value={code}>
                {currencyLabel(code)}
              </option>
            ))}
          </select>
        )}
      </FormField>

      <FormField
        id="settings-locale"
        name="defaultLocale"
        label={t('settings.form.locale')}
        error={message(errors.defaultLocale)}
      >
        {(control) => (
          <select
            {...control}
            value={values.defaultLocale}
            onChange={(event) => {
              setSaved(false);
              setValue('defaultLocale', event.target.value);
            }}
          >
            {locales.map((code) => (
              <option key={code} value={code}>
                {localeLabel(code)}
              </option>
            ))}
          </select>
        )}
      </FormField>

      <FormField
        id="settings-email"
        name="contactEmail"
        label={t('settings.form.contactEmail')}
        error={message(errors.contactEmail)}
      >
        {(control) => (
          <input
            {...control}
            type="email"
            autoComplete="off"
            value={values.contactEmail}
            onChange={(event) => {
              setSaved(false);
              setValue('contactEmail', event.target.value);
            }}
          />
        )}
      </FormField>

      <FormField
        id="settings-phone"
        name="contactPhone"
        label={t('settings.form.contactPhone')}
        hint={t('settings.form.contactPhoneHint')}
        error={message(errors.contactPhone)}
      >
        {(control) => (
          <input
            {...control}
            type="tel"
            autoComplete="off"
            value={values.contactPhone}
            onChange={(event) => {
              setSaved(false);
              setValue('contactPhone', event.target.value);
            }}
          />
        )}
      </FormField>

      <FormField
        id="settings-color"
        name="primaryColor"
        label={t('settings.form.primaryColor')}
        hint={colorHint}
        error={message(errors.primaryColor)}
      >
        {(control) => (
          <>
            <div className="color-row">
              <input
                type="color"
                aria-label={t('settings.form.colorPicker')}
                value={colorIsHex ? values.primaryColor.toLowerCase() : '#000000'}
                onChange={(event) => {
                  setSaved(false);
                  setValue('primaryColor', event.target.value);
                }}
              />
              <input
                {...control}
                type="text"
                autoComplete="off"
                spellCheck={false}
                value={values.primaryColor}
                onChange={(event) => {
                  setSaved(false);
                  setValue('primaryColor', event.target.value);
                }}
              />
            </div>
            {colorIsHex ? (
              <p className="color-preview-row">
                <span>{t('settings.color.preview')}</span>
                <span
                  className="color-preview"
                  style={{
                    backgroundColor: values.primaryColor,
                    borderColor: values.primaryColor,
                    color: readableTextColor(values.primaryColor),
                  }}
                >
                  {t('settings.color.previewButton')}
                </span>
              </p>
            ) : null}
          </>
        )}
      </FormField>

      <div className="field-check">
        <input
          id="settings-demo"
          name="isDemo"
          type="checkbox"
          checked={isDemo}
          onChange={(event) => {
            setSaved(false);
            setIsDemo(event.target.checked);
          }}
        />
        <label htmlFor="settings-demo">{t('settings.form.demo')}</label>
      </div>

      <p className="field-hint">{t('settings.form.reloadNote')}</p>

      <div className="form-actions">
        <button type="submit" disabled={save.isPending}>
          {save.isPending ? t('settings.form.saving') : t('settings.form.save')}
        </button>
      </div>
      <p role="status">{saved ? t('settings.saved') : ''}</p>

      <ConfirmDialog
        open={asking?.kind === 'currency'}
        title={t('settings.currencyDialog.title')}
        confirmLabel={t('settings.currencyDialog.confirm')}
        cancelLabel={t('settings.currencyDialog.cancel')}
        onConfirm={answerYes}
        onCancel={() => {
          setAsking(null);
        }}
      >
        <p>{t('settings.currencyDialog.message')}</p>
      </ConfirmDialog>
      <ConfirmDialog
        open={asking?.kind === 'demo'}
        title={t('settings.demoDialog.title')}
        confirmLabel={t('settings.demoDialog.confirm')}
        cancelLabel={t('settings.demoDialog.cancel')}
        onConfirm={answerYes}
        onCancel={() => {
          setAsking(null);
        }}
      >
        <p>{t('settings.demoDialog.message')}</p>
      </ConfirmDialog>
    </form>
  );
}

/** The screen of the shop settings: loads them with their version and shows the form. */
export function SettingsPage() {
  const query = useAdminStoreSettings();
  // A new key builds the form again from the settings that were just loaded (after a version conflict).
  const [formKey, setFormKey] = useState(0);

  if (query.isPending) {
    return <p role="status">{t('settings.loading')}</p>;
  }
  if (query.isError) {
    return (
      <>
        <p role="alert" className="form-error">
          {t('settings.error.loadFailed')}
        </p>
        <button
          type="button"
          onClick={() => {
            void query.refetch();
          }}
        >
          {t('settings.retry')}
        </button>
      </>
    );
  }

  return (
    <SettingsForm
      key={formKey}
      settings={query.data}
      onReload={() => {
        void query.refetch().then((result) => {
          if (result.isSuccess) {
            setFormKey((key) => key + 1);
          }
        });
      }}
    />
  );
}
