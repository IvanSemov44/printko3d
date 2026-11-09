// The official Bulgarian transliteration (Закон за транслитерацията, 2009), letter by letter.
const LETTERS: Readonly<Record<string, string>> = {
  а: 'a',
  б: 'b',
  в: 'v',
  г: 'g',
  д: 'd',
  е: 'e',
  ж: 'zh',
  з: 'z',
  и: 'i',
  й: 'y',
  к: 'k',
  л: 'l',
  м: 'm',
  н: 'n',
  о: 'o',
  п: 'p',
  р: 'r',
  с: 's',
  т: 't',
  у: 'u',
  ф: 'f',
  х: 'h',
  ц: 'ts',
  ч: 'ch',
  ш: 'sh',
  щ: 'sht',
  ъ: 'a',
  ь: 'y',
  ю: 'yu',
  я: 'ya',
};

export const SLUG_MAX_LENGTH = 200;

/** The same rule as the API: lower-case letters and digits, with single hyphens between them. */
export const SLUG_FORMAT = /^[a-z0-9]+(-[a-z0-9]+)*$/;

/** A slug suggested from a name: Cyrillic is transliterated, everything else becomes a hyphen. */
export function slugFromName(name: string): string {
  const latin = name.toLowerCase().replace(/[а-я]/g, (letter) => LETTERS[letter] ?? letter);
  return latin
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, SLUG_MAX_LENGTH)
    .replace(/-+$/g, '');
}
