import { describe, expect, it } from 'vitest';
import {
  UnexpectedResponseError,
  field,
  readArray,
  readBoolean,
  readInteger,
  readNullable,
  readObject,
  readOneOf,
  readString,
  readStringMap,
} from './decode';

describe('decode', () => {
  it('reads strings, booleans and objects of the right shape', () => {
    expect(readString('a', 'x')).toBe('a');
    expect(readBoolean(false, 'x')).toBe(false);
    expect(readObject({ a: 1 }, 'x')).toEqual({ a: 1 });
  });

  it('names the field when the shape is wrong', () => {
    expect(() => readString(5, 'product.name')).toThrow(UnexpectedResponseError);
    expect(() => readString(5, 'product.name')).toThrow('product.name');
    expect(() => readBoolean('yes', 'x')).toThrow(UnexpectedResponseError);
    expect(() => readObject(null, 'x')).toThrow(UnexpectedResponseError);
    expect(() => readObject([], 'x')).toThrow(UnexpectedResponseError);
  });

  it('reads an integer from a number or a numeric string', () => {
    expect(readInteger(12, 'x')).toBe(12);
    expect(readInteger('12', 'x')).toBe(12);
    expect(readInteger(-3, 'x')).toBe(-3);
  });

  it('rejects an integer that is missing, fractional, not numeric or not safe', () => {
    expect(() => readInteger(undefined, 'x')).toThrow(UnexpectedResponseError);
    expect(() => readInteger(1.5, 'x')).toThrow(RangeError);
    expect(() => readInteger('abc', 'variant.price')).toThrow('variant.price');
    expect(() => readInteger(Number.MAX_SAFE_INTEGER + 2, 'x')).toThrow(RangeError);
  });

  it('reads null for nullable fields, and checks the value otherwise', () => {
    const readName = readNullable(readString);
    expect(readName(null, 'x')).toBeNull();
    expect(readName('a', 'x')).toBe('a');
    expect(() => readName(undefined, 'x')).toThrow(UnexpectedResponseError);
  });

  it('accepts only the listed values', () => {
    const readStatus = readOneOf(['Draft', 'Published'] as const);
    expect(readStatus('Draft', 'x')).toBe('Draft');
    expect(() => readStatus('Deleted', 'status')).toThrow('status');
    expect(() => readStatus(1, 'x')).toThrow(UnexpectedResponseError);
  });

  it('reads arrays item by item and names the bad index', () => {
    const readNames = readArray(readString);
    expect(readNames(['a', 'b'], 'x')).toEqual(['a', 'b']);
    expect(() => readNames(['a', 2], 'names')).toThrow('names[1]');
    expect(() => readNames({}, 'x')).toThrow(UnexpectedResponseError);
  });

  it('reads a map of strings', () => {
    expect(readStringMap({ color: 'blue' }, 'x')).toEqual({ color: 'blue' });
    expect(() => readStringMap({ color: 1 }, 'attributes')).toThrow('attributes.color');
  });

  it('reads one field of an object', () => {
    expect(field({ name: 'a' }, 'name', readString, 'product')).toBe('a');
    expect(() => field({}, 'name', readString, 'product')).toThrow('product.name');
  });
});
