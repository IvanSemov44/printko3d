import { describe, expect, it } from 'vitest';
import { validateCheckout, type CheckoutValues } from './checkoutValidation';

const VALID: CheckoutValues = {
  name: 'Test Buyer',
  email: 'buyer@example.test',
  phone: '+359 88 000 0000',
  street: '1 Test Street',
  city: 'Sofia',
  postalCode: '1000',
};

describe('validateCheckout', () => {
  it('accepts a complete form with the terms accepted', () => {
    expect(validateCheckout(VALID, true)).toEqual({});
  });

  it('names every empty field, also when it only holds spaces', () => {
    const errors = validateCheckout(
      { name: '', email: ' ', phone: '', street: '   ', city: '', postalCode: '' },
      true,
    );

    expect(Object.keys(errors).sort()).toEqual([
      'city',
      'email',
      'name',
      'phone',
      'postalCode',
      'street',
    ]);
    expect(errors.name).toBe('checkout.error.required');
  });

  it.each(['not-an-email', 'a@b', 'two words@example.test', '@example.test'])(
    'refuses the email %s',
    (email) => {
      expect(validateCheckout({ ...VALID, email }, true)).toEqual({
        email: 'checkout.error.email',
      });
    },
  );

  it.each(['abc', '12345', '+359 88', 'phone 0888123456'])('refuses the phone %s', (phone) => {
    expect(validateCheckout({ ...VALID, phone }, true)).toEqual({
      phone: 'checkout.error.phone',
    });
  });

  it.each(['0888 123 456', '+359(88)1234567', '02-123-4567'])('accepts the phone %s', (phone) => {
    expect(validateCheckout({ ...VALID, phone }, true)).toEqual({});
  });

  it.each(['100', '10000', '10a0', '1 00'])('refuses the postal code %s', (postalCode) => {
    expect(validateCheckout({ ...VALID, postalCode }, true)).toEqual({
      postalCode: 'checkout.error.postalCode',
    });
  });

  it('refuses a text that is longer than the server allows', () => {
    const errors = validateCheckout(
      { ...VALID, name: 'n'.repeat(201), street: 's'.repeat(201), city: 'c'.repeat(101) },
      true,
    );

    expect(errors).toEqual({
      name: 'checkout.error.tooLong',
      street: 'checkout.error.tooLong',
      city: 'checkout.error.tooLong',
    });
  });

  it('wants the terms accepted', () => {
    expect(validateCheckout(VALID, false)).toEqual({ termsAccepted: 'checkout.error.terms' });
  });

  it('judges the trimmed value, as the server does', () => {
    expect(validateCheckout({ ...VALID, postalCode: ' 1000 ', name: '  Test  ' }, true)).toEqual(
      {},
    );
  });
});
