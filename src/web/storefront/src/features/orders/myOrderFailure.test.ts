import { ApiError } from '@storefront/shared';
import { describe, expect, it } from 'vitest';
import { looksLikeEmail, myOrderFailure, parseOrderNumber } from './myOrderFailure';
import { ORDER_STEPS, statusText, stepIndex } from './orderStatus';

describe('parseOrderNumber', () => {
  it.each([
    ['12345678', 12345678],
    ['PR-12345678', 12345678],
    ['pr-12345678', 12345678],
    ['PR 12345678', 12345678],
    ['  PR-12345678  ', 12345678],
  ])('reads %s as %d', (typed, expected) => {
    expect(parseOrderNumber(typed)).toBe(expected);
  });

  it.each(['', '1234567', '123456789', 'PR-', 'abc', '1234 5678', 'PR-1234567x'])(
    'refuses %s',
    (typed) => {
      expect(parseOrderNumber(typed)).toBeNull();
    },
  );
});

describe('looksLikeEmail', () => {
  it.each(['a@b.co', ' buyer@example.test '])('accepts %s', (typed) => {
    expect(looksLikeEmail(typed)).toBe(true);
  });

  it.each(['', 'buyer', 'buyer@', '@example.test', 'buyer@example', 'bu yer@example.test'])(
    'refuses %s',
    (typed) => {
      expect(looksLikeEmail(typed)).toBe(false);
    },
  );
});

describe('myOrderFailure', () => {
  it('says the same thing for every refusal of a wrong order, whatever was wrong', () => {
    expect(myOrderFailure(new ApiError(404, 'order.not_found'))).toBe('myOrder.error.notFound');
  });

  it('tells the buyer to wait when there were too many requests', () => {
    expect(myOrderFailure(new ApiError(429, null))).toBe('myOrder.error.tooMany');
  });

  it.each([
    new ApiError(500, null),
    new ApiError(400, 'validation.failed'),
    new TypeError('offline'),
  ])('gives the general text for anything else, and never what the server said', (error) => {
    expect(myOrderFailure(error)).toBe('myOrder.error.generic');
  });
});

describe('order status', () => {
  it('has a text of its own for every status, and a neutral one for a status it does not know', () => {
    expect(statusText('Shipped')).toBe('myOrder.status.Shipped');
    expect(statusText('Teleported')).toBe('myOrder.status.unknown');
    expect(statusText('toString')).toBe('myOrder.status.unknown');
  });

  it('puts the steps in the order of the lifecycle, without the cancelled one', () => {
    expect(ORDER_STEPS).toEqual([
      'New',
      'Paid',
      'InProduction',
      'ReadyToShip',
      'Shipped',
      'Completed',
    ]);
    expect(stepIndex('Paid')).toBe(1);
    expect(stepIndex('Cancelled')).toBe(-1);
    expect(stepIndex('Teleported')).toBe(-1);
  });
});
