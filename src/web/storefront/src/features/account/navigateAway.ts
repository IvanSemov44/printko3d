/** Opens an address as a whole new page: the way to a social provider, which asks the buyer on its own page. A test replaces it. */
export function navigateAway(address: string): void {
  window.location.assign(address);
}
