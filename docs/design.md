<!--
Source of truth for the look of the store and the admin panel. Adopted by decision 0023 (docs/decisions/0023-design-system.md).
Order of precedence on conflict: CLAUDE.md -> docs/decisions -> docs/handbook -> this file.
The corrections below win over the rest of this file.
-->

# Corrections (decision 0023)

- **Primary colour is a default, not a constant.** `--color-primary` comes from the store settings; the orange in section 2 is the default when none is set. Text on primary is computed (`readableTextColor`), never hardcoded.
- **No store name, price format or currency in components.** The wordmark comes from the settings. Prices go through `money.ts` with the currency from the settings; `24,90 €` below is an example of the output, not a literal.
- **All user-facing text is in the locale files.** Bulgarian strings in this file are copy guidance, not code.
- **Fonts are self-hosted** (`woff2` files in the repo, OFL licence, Cyrillic and Latin subsets). No request to Google Fonts: it sends the visitor's IP to a third party and needs a CSP exception.
- **Order statuses are the seven from `docs/requirements.md`:** Нова, Платена, В производство, Готова за изпращане, Изпратена, Завършена, Отказана. "В печат" in section 11 means "В производство". Section 11 colours are extended to all seven.
- **Quick add-to-cart on a product card** only for a product with a single variant; otherwise the card links to the product page.
- **Not built yet, do not show:** header search, admin "Табло", "Клиенти" and "Поръчки по идея". Add each with the task that builds the feature.
- **Dark theme** is not in scope for the first release. Write tokens so it can be added; do not build it.
- **Checkout, order confirmation, account pages, 404/error pages, footer, cookie banner, toasts, pagination, filters and loading skeletons** are not described here yet. Extend this file with the task that builds each one, reusing the patterns above.

---

# DESIGN.md — Принтко 3Д

> Design system for the Принтко 3Д online store (3D-printed puzzles, figurines and made-to-order prints).
> AI agents: read this file before generating or changing any UI. When something is not covered here, extend the existing patterns rather than inventing new ones.

---

## 1. Visual Theme & Atmosphere

**"The friendly workshop."** The store should feel like a small maker studio: warm, tactile, a little playful, clearly handmade by real people — but tidy and trustworthy enough to pay online.

- **Mood:** warm paper background, dark ink text, one bold filament-orange accent.
- **Signature motif — layer lines.** 3D prints are built from stacked layers. We echo this with:
  - subtle horizontal stripe textures in the hero and section dividers;
  - a hard, offset "stacked" shadow on interactive cards (no blur).
- **Products are the color.** The UI stays mostly neutral so colorful prints stand out. Bright colors appear in the product photos and filament swatches, not in the chrome.
- **Not:** dark "gamer" neon, glassmorphism, heavy gradients, generic SaaS blue.

UI language is **Bulgarian** (Cyrillic). Every font and layout decision must work with Cyrillic text, which runs ~10–15% longer than English.

---

## 2. Color Palette & Roles

Define all colors as CSS custom properties. Never hardcode hex values in components.

### Light theme (default)

| Token                   | Hex       | Role                                                         |
| ----------------------- | --------- | ------------------------------------------------------------ |
| `--color-bg`            | `#FAF7F2` | Page background ("paper")                                    |
| `--color-surface`       | `#FFFFFF` | Cards, drawers, inputs                                       |
| `--color-surface-alt`   | `#F1ECE4` | Product image backdrop, subtle panels                        |
| `--color-ink`           | `#1C1A17` | Primary text, hard shadows, icons                            |
| `--color-ink-muted`     | `#6B655C` | Secondary text, captions, placeholders                       |
| `--color-border`        | `#E3DCD1` | Dividers, input borders, card outlines                       |
| `--color-primary`       | `#FF6B2C` | Filament orange — primary buttons, key highlights            |
| `--color-primary-hover` | `#F25A18` | Hover/pressed primary                                        |
| `--color-on-primary`    | `#1C1A17` | Text on primary (dark ink, NOT white — white fails contrast) |
| `--color-secondary`     | `#0F766E` | Nozzle teal — links, "made to order", secondary actions      |
| `--color-on-secondary`  | `#FFFFFF` | Text on secondary                                            |

### Status

| Token             | Hex       | Use                         |
| ----------------- | --------- | --------------------------- |
| `--color-success` | `#15803D` | In stock, order confirmed   |
| `--color-warning` | `#B45309` | Low stock, longer lead time |
| `--color-error`   | `#B91C1C` | Form errors, failed payment |
| `--color-info`    | `#1D4ED8` | Neutral notices             |

### Filament accents (decorative only)

Used for category tags, illustrations and filament swatches. Never for text or core UI states.

`#FFC531` yellow · `#3B82F6` blue · `#F472B6` pink · `#22C55E` green · `#A855F7` purple

### Dark theme

| Token                  | Hex       |
| ---------------------- | --------- |
| `--color-bg`           | `#15130F` |
| `--color-surface`      | `#1F1C17` |
| `--color-surface-alt`  | `#2A261F` |
| `--color-ink`          | `#F3EEE6` |
| `--color-ink-muted`    | `#A39C90` |
| `--color-border`       | `#34302A` |
| `--color-primary`      | `#FF7A3D` |
| `--color-secondary`    | `#2DD4BF` |
| `--color-on-secondary` | `#15130F` |

In dark mode the hard shadow uses `#000000` at 60% opacity instead of ink.

---

## 3. Typography Rules

All three fonts are on Google Fonts and support Cyrillic.

| Role                   | Font               | Weights            |
| ---------------------- | ------------------ | ------------------ |
| Display / headings     | **Unbounded**      | 600, 700           |
| Body / UI              | **Manrope**        | 400, 500, 600, 700 |
| Specs / technical data | **JetBrains Mono** | 400, 500           |

### Scale (rem, base 16px)

| Token     | Size / line-height | Font                                     | Use                               |
| --------- | ------------------ | ---------------------------------------- | --------------------------------- |
| `display` | 3.5 / 1.05         | Unbounded 700                            | Home hero only                    |
| `h1`      | 2.5 / 1.1          | Unbounded 700                            | Page titles                       |
| `h2`      | 1.75 / 1.2         | Unbounded 600                            | Section titles                    |
| `h3`      | 1.25 / 1.3         | Manrope 700                              | Card titles, sub-sections         |
| `body-lg` | 1.125 / 1.6        | Manrope 400                              | Product descriptions              |
| `body`    | 1 / 1.6            | Manrope 400                              | Default text                      |
| `small`   | 0.875 / 1.5        | Manrope 500                              | Captions, helper text             |
| `label`   | 0.75 / 1.4         | Manrope 700, uppercase, +0.06em tracking | Badges, overlines                 |
| `mono`    | 0.875 / 1.5        | JetBrains Mono 400                       | Dimensions, weight, material, SKU |

Rules:

- Unbounded is wide — limit it to headings of max ~6 words. On mobile, step `display` down to 2.25rem and `h1` to 1.875rem.
- Never use Unbounded for body text, buttons or prices.
- Technical specs (e.g. `12 × 8 × 5 см`, `PLA`, `0,2 мм слой`) always use the mono style.

---

## 4. Spacing, Radius & Elevation

**Spacing scale (4px base):** `4, 8, 12, 16, 24, 32, 48, 64, 96`. Use only these values.

**Radius:**

- `--radius-sm: 8px` — inputs, badges, small buttons
- `--radius-md: 12px` — buttons, cards
- `--radius-lg: 20px` — drawers, modals, hero panels
- `--radius-full` — filament swatches, avatar, pill tags

**Elevation — the "stacked layer" shadow:**

- Resting card: `1px solid var(--color-border)`, no shadow.
- Hover / focus card: `box-shadow: 4px 4px 0 var(--color-ink)` and `transform: translate(-2px, -2px)`.
- Pressed: shadow `2px 2px 0`, translate `(0, 0)`.
- Drawers and modals: one soft shadow only — `0 12px 40px rgba(28, 26, 23, 0.18)`.
- Never use soft blurred shadows on cards.

**Layer-line texture** (hero, footer, empty states):

```css
background-image: repeating-linear-gradient(
  0deg,
  transparent 0 5px,
  rgba(28, 26, 23, 0.04) 5px 6px
);
```

**Motion:** 150ms `ease-out` for hovers, 250ms for drawers. Respect `prefers-reduced-motion` (disable translate effects).

---

## 5. Component Stylings

### Buttons

- **Primary:** bg `--color-primary`, text `--color-on-primary`, Manrope 700, height 48px, padding `0 24px`, radius md, 2px solid ink border. Hover → stacked shadow. Use for "Добави в количката", "Поръчай".
- **Secondary:** transparent bg, 2px solid ink border, ink text. Same sizing.
- **Ghost / text:** teal text, underline on hover. For "Виж всички", tertiary actions.
- **Destructive:** text `--color-error`, only inside confirmations.
- Disabled: 40% opacity, no shadow, `cursor: not-allowed`.
- Max one primary button per view section.

### Product card

- Container: surface bg, 1px border, radius md, stacked shadow on hover; entire card is the link.
- Image: square 1:1, on `--color-surface-alt`, `object-fit: contain`, 16px inner padding (products on a clean backdrop, not cropped).
- Availability badge, top-left over the image (see Badges).
- Below: title (h3, max 2 lines with ellipsis), one mono spec line (material · size), price.
- Price: Manrope 700, 1.25rem, ink. Old price struck-through in muted.
- Quick "add to cart" icon button bottom-right appears on hover (always visible on touch devices).

### Badges

Pill, `label` type style, 4px 10px padding.

- **В наличност** — success green bg tint (`#15803D` at 12%), green text.
- **По поръчка** — teal tint, teal text, plus lead time in small text on the product page: "Изработка до 3 работни дни".
- **Последни бройки** — warning tint.
- **Ново** — yellow filament accent bg, ink text.

### Filament / color selector

Row of 32px circular swatches with 2px border; selected swatch gets a 2px ink ring offset by 3px. Color name shown below the row ("Цвят: Небесно син"). Unavailable colors: diagonal strike line, not clickable.

### Inputs & forms

- Height 48px, surface bg, 1.5px border, radius sm, Manrope 400 1rem (never smaller on mobile — avoids iOS zoom).
- Focus: border teal + 3px teal ring at 25% opacity.
- Labels always visible above the field (no placeholder-only labels).
- Errors: red border, message below in small red text, written in plain Bulgarian ("Въведи валиден имейл").

### Header

- Sticky, surface bg, bottom border. Height 64px desktop / 56px mobile.
- Left: wordmark "Принтко 3Д" in Unbounded 700. Center (desktop): category links. Right: search, cart with count bubble (primary bg).
- Mobile: hamburger opens full-height drawer from the left.

### Cart drawer

- Slides from the right, 420px wide desktop / full width mobile.
- Line items: 64px thumbnail, title, color, quantity stepper, price.
- Sticky bottom: subtotal, delivery note, primary "Към плащане" button.

### Demo mode banner

While the store runs with demo data, show a full-width strip above the header: yellow filament accent bg, ink text, small: "Демо версия — поръчките не се обработват." Must not be dismissible.

### Empty states

Layer-line texture background, a simple line illustration, one sentence, one action. Example: "Количката ти е празна." + "Разгледай каталога".

---

## 6. Layout Principles

- Max content width **1200px**, centered, side gutter 16px mobile / 24px tablet / 32px desktop.
- 12-column grid, 24px gap on desktop.
- Product grid: **2 columns** mobile, **3** tablet, **4** desktop; 16px / 24px gaps.
- Product page: gallery left (7 cols), buy box right (5 cols, sticky on desktop). Mobile: gallery → title/price → selector → button → description → specs.
- Vertical rhythm between page sections: 64px mobile / 96px desktop.
- Hero: left-aligned text, product imagery on the right; no full-bleed stock photos.

---

## 7. Content & Formatting

- Tone: warm, direct, informal **"ти"** form. Short sentences. No corporate filler.
- Prices in euro, Bulgarian format: `24,90 €` (comma decimal, space, symbol after).
- Dates: `10.11.2026`. Dimensions: `12 × 8 × 5 см`.
- Button text is a verb: "Добави в количката", "Поръчай", "Качи модел".
- Material names stay as-is: PLA, PETG, TPU.

---

## 8. Responsive Behavior

| Breakpoint | Min width |
| ---------- | --------- |
| `sm`       | 640px     |
| `md`       | 768px     |
| `lg`       | 1024px    |
| `xl`       | 1280px    |

- Mobile-first CSS.
- Touch targets ≥ 44 × 44px.
- No horizontal page scroll at 360px width.
- Hover-only affordances must have a visible touch alternative.

---

## 9. Accessibility

- Text contrast ≥ 4.5:1 (large text ≥ 3:1). Primary buttons use dark text on orange for this reason.
- Visible focus ring on every interactive element (teal ring, never `outline: none` without a replacement).
- All product images have Bulgarian alt text describing the item and color.
- Color is never the only signal: availability badges always include text.

---

## 10. Do's and Don'ts

**Do**

- Keep the chrome neutral and let product photos bring the color.
- Use the stacked hard shadow for interactive cards and primary buttons.
- Use mono type for every technical spec.
- Reuse existing tokens and components before creating new ones.

**Don't**

- Don't use white text on the orange primary.
- Don't use gradients, glass effects or blurred card shadows.
- Don't put Unbounded in body text, buttons or prices.
- Don't use the filament accent colors for text or form states.
- Don't use more than one primary button per section.
- Don't use stock photos of people or generic "tech" imagery.

---

## 11. Admin Panel

The admin panel (orders, products, custom requests) uses the **same tokens** as the store, in a denser, calmer "work mode". The store should delight customers; the admin should be fast for the owner.

### What stays the same

Colors, Manrope body, JetBrains Mono for numbers/SKUs/order IDs, availability badges, buttons, inputs, focus rings, demo banner.

### What changes

- **Density:** base text 14px (`small`), spacing mostly 8–16px, table rows 44px, inputs 40px high (touch targets still ≥ 44px on mobile).
- **No playfulness:** Unbounded only for the wordmark in the sidebar. Page titles use Manrope 700, 24px. No layer-line texture, no lift/stacked shadow on hover; rows get `--color-surface-alt` background on hover instead.
- **Background:** page `--color-bg`, content panels `--color-surface` with 1px border, radius md.

### Layout

- **Sidebar** 240px, fixed on the left, surface bg, right border. Wordmark + "Админ" label on top, then nav: Табло, Поръчки, Продукти, Поръчки по идея, Клиенти, Настройки. Active item: `--color-surface-alt` bg, ink text, 3px primary bar on the left edge. On screens < 1024px the sidebar collapses into a top bar with a menu button.
- **Content:** max-width 1280px, 32px padding. Page header row: title left, primary action right ("Нов продукт").

### Order statuses

Pill badges (same shape as store badges, sentence case, not uppercase):

| Status    | Background       | Text      |
| --------- | ---------------- | --------- |
| Нова      | `#1D4ED8` at 12% | `#1D4ED8` |
| В печат   | `#FF6B2C` at 18% | `#C2410C` |
| Готова    | `#15803D` at 12% | `#15803D` |
| Изпратена | `#0F766E` at 12% | `#0F766E` |
| Отказана  | `#B91C1C` at 10% | `#B91C1C` |

"В печат" is the owner's key state — it shows what is on the printers right now.

### Data tables (main admin component)

- Header row: `label` style (12px, 700, uppercase, muted), surface-alt bg, sticky.
- Rows 44px, 1px bottom border, hover surface-alt. Whole row clickable to the detail page.
- Numbers, prices, dates and IDs: JetBrains Mono, right-aligned (prices) or left (IDs).
- Toolbar above the table: search input (left), status filter tabs, secondary actions (right).
- Status filter tabs: pills with counts ("В печат 3"); active tab = ink bg, white text.
- Bulk actions bar appears only when rows are selected.
- Empty table: one sentence + one action, no illustration.
- Wide tables scroll horizontally inside their panel, never the page.

### Stat tiles (dashboard)

Surface panel, label on top, value in Manrope 700 28px, optional muted delta line below. Max 4 per row.

### Forms (product edit)

Two-column layout on desktop: main fields left (2/3), side panel right (1/3: status, price, stock, availability type). Sections in separate panels with an `h3` title. Sticky bottom bar with "Отказ" (secondary) and "Запази" (primary) when there are unsaved changes.

---

## 12. Agent Prompt Guide

Example prompts that work with this file:

- "Build the product listing page following DESIGN.md: 4-column grid, product cards with availability badges and stacked hover shadow."
- "Create the product page buy box per DESIGN.md: title, price in euro format, filament color selector, primary add-to-cart button, mono spec list."
- "Add the cart drawer from DESIGN.md section 5 with a sticky subtotal footer."
- "Check this component against DESIGN.md and list every token or rule it violates."
- "Build the admin orders list per DESIGN.md section 11: sidebar, status filter tabs with counts, data table with mono IDs and right-aligned prices."
- "Create the admin product edit form per DESIGN.md section 11 with a sticky save bar."
