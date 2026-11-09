export type TranslationParams = Readonly<Record<string, string | number>>;

/**
 * A missing key returns the key itself, so a gap in the translations never leaves an empty screen.
 * `{name}` in a message is replaced from `params`; an unknown placeholder stays as it is.
 */
export function createTranslator<K extends string>(messages: Readonly<Record<K, string>>) {
  return (key: K, params?: TranslationParams): string => {
    const text = Object.hasOwn(messages, key) ? messages[key] : key;
    if (!params) {
      return text;
    }
    return text.replace(/\{(\w+)\}/g, (placeholder, name: string) =>
      Object.hasOwn(params, name) ? String(params[name]) : placeholder,
    );
  };
}
