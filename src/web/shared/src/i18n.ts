/** A missing key returns the key itself, so a gap in the translations never leaves an empty screen. */
export function createTranslator<K extends string>(messages: Readonly<Record<K, string>>) {
  return (key: K): string => (Object.hasOwn(messages, key) ? messages[key] : key);
}
