export function createTranslator<K extends string>(messages: Record<K, string>) {
  return (key: K): string => messages[key];
}
