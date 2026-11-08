/** Shown instead of a product image when there is none or it cannot be loaded. Decorative, so hidden from screen readers. */
export function ImagePlaceholder() {
  return (
    <svg
      className="image-placeholder"
      data-testid="image-placeholder"
      viewBox="0 0 100 100"
      preserveAspectRatio="xMidYMid slice"
      aria-hidden="true"
      focusable="false"
    >
      <rect width="100" height="100" fill="currentColor" opacity="0.08" />
      <circle cx="68" cy="34" r="8" fill="currentColor" opacity="0.25" />
      <path d="M12 78 38 46 56 68 66 56 88 78Z" fill="currentColor" opacity="0.25" />
    </svg>
  );
}
