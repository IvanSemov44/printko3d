/** Intl separates number and symbol with a non-breaking space; compare with a plain one. */
export function plain(text: string | null): string {
  return (text ?? '').replace(new RegExp('[  ]', 'g'), ' ');
}
