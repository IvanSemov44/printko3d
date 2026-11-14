import { describe, expect, it } from 'vitest';
import { isOwnPath } from './usePayment';

describe('isOwnPath', () => {
  it.each(['/pay/test/test_1_abc', '/order/12345678', '/'])(
    'takes %s as a page of the shop',
    (address) => {
      expect(isOwnPath(address)).toBe(true);
    },
  );

  it.each([
    ['another site without a scheme', '//evil.example/pay'],
    ['another site that a browser reads from a backslash', '/\\evil.example/pay'],
    ['an address with a scheme', 'https://pay.example/session'],
    ['a relative address', 'pay/test/abc'],
    ['nothing', ''],
  ])('does not take %s', (_name, address) => {
    expect(isOwnPath(address)).toBe(false);
  });
});
