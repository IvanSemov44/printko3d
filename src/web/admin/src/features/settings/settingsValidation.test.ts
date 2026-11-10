import { describe, expect, it } from 'vitest';
import { validateSettings, type SettingsFormValues } from './settingsValidation';

const VALID: SettingsFormValues = {
  storeName: 'Принтко 3Д',
  defaultCurrency: 'EUR',
  defaultLocale: 'bg',
  contactEmail: 'shop@example.com',
  contactPhone: '+359 (88) 123-456',
  primaryColor: '#2f5d8a',
};

function check(change: Partial<SettingsFormValues>) {
  return validateSettings({ ...VALID, ...change });
}

describe('validateSettings', () => {
  it('accepts a valid form', () => {
    expect(check({})).toEqual({});
  });

  describe('name', () => {
    it('is required, also when it is only spaces', () => {
      expect(check({ storeName: '' }).storeName).toBe('settings.error.nameRequired');
      expect(check({ storeName: '   ' }).storeName).toBe('settings.error.nameRequired');
    });

    it('may have 200 characters, not 201, counted without the spaces around it', () => {
      expect(check({ storeName: 'a'.repeat(200) }).storeName).toBeUndefined();
      expect(check({ storeName: ` ${'a'.repeat(200)} ` }).storeName).toBeUndefined();
      expect(check({ storeName: 'a'.repeat(201) }).storeName).toBe('settings.error.nameTooLong');
    });
  });

  describe('currency and language', () => {
    it.each(['eur', 'EU', 'EURO', 'USD', ''])('refuses the currency %j', (value) => {
      expect(check({ defaultCurrency: value }).defaultCurrency).toBe(
        'settings.error.currencyInvalid',
      );
    });

    it('accepts both allowed currencies', () => {
      expect(check({ defaultCurrency: 'BGN' }).defaultCurrency).toBeUndefined();
    });

    it.each(['en', 'BG', ''])('refuses the language %j', (value) => {
      expect(check({ defaultLocale: value }).defaultLocale).toBe('settings.error.localeInvalid');
    });
  });

  describe('email', () => {
    it('is required', () => {
      expect(check({ contactEmail: '' }).contactEmail).toBe('settings.error.emailRequired');
    });

    it.each([
      'info',
      'info@',
      'info@example',
      '@example.com',
      'in fo@example.com',
      'a@@b.com',
      'a@b..com',
    ])('refuses the format %j', (value) => {
      expect(check({ contactEmail: value }).contactEmail).toBe('settings.error.emailInvalid');
    });

    it('may have 320 characters, not 321', () => {
      expect(check({ contactEmail: `${'a'.repeat(315)}@x.bg` }).contactEmail).toBeUndefined();
      expect(check({ contactEmail: `${'a'.repeat(316)}@x.bg` }).contactEmail).toBe(
        'settings.error.emailTooLong',
      );
    });
  });

  describe('phone', () => {
    it('is optional', () => {
      expect(check({ contactPhone: '' }).contactPhone).toBeUndefined();
    });

    it('may have 50 characters, not 51', () => {
      expect(check({ contactPhone: '1'.repeat(50) }).contactPhone).toBeUndefined();
      expect(check({ contactPhone: '1'.repeat(51) }).contactPhone).toBe(
        'settings.error.phoneTooLong',
      );
    });

    it.each(['call me', '0888 12x456', '0888/123456'])('refuses %j', (value) => {
      expect(check({ contactPhone: value }).contactPhone).toBe('settings.error.phoneInvalid');
    });
  });

  describe('primary colour', () => {
    it.each(['', '#abc', '112233', '#12345G', '#1122334'])('refuses the format %j', (value) => {
      expect(check({ primaryColor: value }).primaryColor).toBe('settings.error.colorInvalid');
    });

    it('accepts capital letters', () => {
      expect(check({ primaryColor: '#2F5D8A' }).primaryColor).toBeUndefined();
    });

    // The same boundary as in the API tests (StoreSettingsManagementTests).
    it('accepts exactly 3:1 with white and refuses just under it', () => {
      expect(check({ primaryColor: '#949494' }).primaryColor).toBeUndefined();
      expect(check({ primaryColor: '#959595' }).primaryColor).toBe(
        'settings.error.colorLowContrast',
      );
    });

    it('refuses white', () => {
      expect(check({ primaryColor: '#ffffff' }).primaryColor).toBe(
        'settings.error.colorLowContrast',
      );
    });
  });
});
