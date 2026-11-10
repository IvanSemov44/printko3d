import { toSafeInteger } from '../numbers';

/** The API answered with a body that does not have the shape the panel expects. The message says which field. */
export class UnexpectedResponseError extends Error {
  constructor(path: string, expected: string) {
    super(`Unexpected API response: ${path} should be ${expected}`);
    this.name = 'UnexpectedResponseError';
  }
}

/** Reads one field of an answer. A wrong shape throws an UnexpectedResponseError instead of showing NaN or undefined later. */
export type Reader<T> = (value: unknown, path: string) => T;

export const readString: Reader<string> = (value, path) => {
  if (typeof value !== 'string') {
    throw new UnexpectedResponseError(path, 'a string');
  }
  return value;
};

export const readBoolean: Reader<boolean> = (value, path) => {
  if (typeof value !== 'boolean') {
    throw new UnexpectedResponseError(path, 'a boolean');
  }
  return value;
};

/** An integer that may come as a number or as a numeric string (the generated types allow both). */
export const readInteger: Reader<number> = (value, path) => {
  if (typeof value !== 'number' && typeof value !== 'string') {
    throw new UnexpectedResponseError(path, 'an integer');
  }
  try {
    return toSafeInteger(value);
  } catch {
    // A RangeError, as toSafeInteger throws, but now it says which field was wrong.
    throw new RangeError(`Unexpected API response: ${path} should be a safe integer`);
  }
};

export function readNullable<T>(read: Reader<T>): Reader<T | null> {
  return (value, path) => (value === null ? null : read(value, path));
}

export function readOneOf<T extends string>(allowed: readonly T[]): Reader<T> {
  return (value, path) => {
    const found = allowed.find((item) => item === value);
    if (found === undefined) {
      throw new UnexpectedResponseError(path, `one of ${allowed.join(', ')}`);
    }
    return found;
  };
}

export function readArray<T>(read: Reader<T>): Reader<T[]> {
  return (value, path) => {
    if (!Array.isArray(value)) {
      throw new UnexpectedResponseError(path, 'an array');
    }
    return (value as unknown[]).map((item, index) => read(item, `${path}[${String(index)}]`));
  };
}

export function readStringMap(value: unknown, path: string): Record<string, string> {
  const object = readObject(value, path);
  return Object.fromEntries(
    Object.entries(object).map(([key, item]) => [key, readString(item, `${path}.${key}`)]),
  );
}

export function readObject(value: unknown, path: string): Record<string, unknown> {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    throw new UnexpectedResponseError(path, 'an object');
  }
  return value as Record<string, unknown>;
}

/** Reads `object[key]` with the given reader; the path in the error is `parent.key`. */
export function field<T>(
  object: Record<string, unknown>,
  key: string,
  read: Reader<T>,
  path: string,
): T {
  return read(object[key], `${path}.${key}`);
}
