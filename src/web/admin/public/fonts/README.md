# Fonts

Self-hosted so that no request goes to Google Fonts (decision 0023). Variable `woff2` files, Cyrillic and Latin subsets only,
taken from the Google Fonts CDN (`fonts.gstatic.com`) on 2026-11-12.

| Font           | Files                                                         | Licence (SIL OFL 1.1)   |
| -------------- | ------------------------------------------------------------- | ----------------------- |
| Unbounded      | `unbounded-cyrillic.woff2`, `unbounded-latin.woff2`           | `OFL-unbounded.txt`     |
| Manrope        | `manrope-cyrillic.woff2`, `manrope-latin.woff2`               | `OFL-manrope.txt`       |
| JetBrains Mono | `jetbrains-mono-cyrillic.woff2`, `jetbrains-mono-latin.woff2` | `OFL-jetbrainsmono.txt` |

Total size: about 158 KB. The `@font-face` rules are at the top of `src/index.css`.
