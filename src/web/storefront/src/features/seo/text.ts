export const MAX_TITLE_LENGTH = 60;
export const MAX_DESCRIPTION_LENGTH = 160;
const ELLIPSIS = '…';

/** Collapses runs of whitespace (descriptions may contain line breaks) into single spaces. */
function normalize(text: string): string {
  return text.replace(/\s+/g, ' ').trim();
}

/** Shortens text to at most `max` characters, cutting at a word boundary and adding "…" when it was cut. */
export function truncate(text: string, max: number): string {
  const clean = normalize(text);
  if (clean.length <= max) {
    return clean;
  }
  const room = clean.slice(0, max - ELLIPSIS.length + 1);
  const lastSpace = room.lastIndexOf(' ');
  // A single very long word has no boundary: cut inside it.
  const cut = lastSpace > 0 ? room.slice(0, lastSpace) : room.slice(0, max - ELLIPSIS.length);
  return `${cut.replace(/[\s,.;:–-]+$/, '')}${ELLIPSIS}`;
}

/** `{page} – {store}`; the page part is shortened so the whole title fits, the store name is never cut off. */
export function buildTitle(storeName: string, page?: string): string {
  const store = normalize(storeName);
  const name = page === undefined ? '' : normalize(page);
  if (name === '') {
    return truncate(store, MAX_TITLE_LENGTH);
  }
  const suffix = ` – ${store}`;
  const room = MAX_TITLE_LENGTH - suffix.length;
  if (room < 10) {
    return truncate(`${name}${suffix}`, MAX_TITLE_LENGTH);
  }
  return `${truncate(name, room)}${suffix}`;
}

/** A meta description of at most 160 characters. */
export function buildDescription(text: string): string {
  return truncate(text, MAX_DESCRIPTION_LENGTH);
}
