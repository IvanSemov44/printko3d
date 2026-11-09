import { describe, expect, it } from 'vitest';
import { validateLogin } from './loginValidation';

describe('validateLogin', () => {
  it('accepts an email and a password', () => {
    expect(validateLogin({ email: 'admin@example.test', password: 'x' })).toEqual({});
  });

  it('asks for both fields when they are empty', () => {
    expect(validateLogin({ email: '  ', password: '' })).toEqual({
      email: 'auth.error.emailRequired',
      password: 'auth.error.passwordRequired',
    });
  });

  it.each(['admin', 'admin@', '@example.test', 'admin@example', 'ad min@example.test'])(
    'rejects the email "%s"',
    (email) => {
      expect(validateLogin({ email, password: 'x' })).toEqual({ email: 'auth.error.emailInvalid' });
    },
  );

  it('does not judge the password beyond it being present', () => {
    expect(validateLogin({ email: 'admin@example.test', password: ' ' })).toEqual({});
  });
});
