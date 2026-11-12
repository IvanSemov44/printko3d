import { useStoreSettings, useT } from '../store-settings/useStoreSettings';

function toTelHref(phone: string): string {
  return `tel:${phone.replace(/[^\d+]/g, '')}`;
}

export function Footer() {
  const { storeName, contactEmail, contactPhone } = useStoreSettings();
  const t = useT();
  const hasContacts = contactEmail !== '' || contactPhone !== '';

  return (
    <footer className="site-footer">
      <p className="site-footer-name">{storeName}</p>
      {hasContacts && (
        <address aria-label={t('layout.footerNav')}>
          {contactEmail !== '' && (
            <p>
              {t('layout.contactEmail')}: <a href={`mailto:${contactEmail}`}>{contactEmail}</a>
            </p>
          )}
          {contactPhone !== '' && (
            <p>
              {t('layout.contactPhone')}: <a href={toTelHref(contactPhone)}>{contactPhone}</a>
            </p>
          )}
        </address>
      )}
    </footer>
  );
}
