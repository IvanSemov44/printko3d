## Задача

Инсталирай `@testing-library/user-event`, включи седмичните обновления на пакетите с `dependabot.yml` и провери дали `eslint-plugin-jsx-a11y` може да се ползва. Без промяна на поведението на приложенията.

## Контекст

- [tech-debt.md](../tech-debt.md) D28: тестовете ползват `fireEvent` на около 135 места, а Testing Library препоръчва `userEvent`. Пакетът е одобрен на 2026-11-11.
- Преглед на `src/web` (2026-11-12): няма автоматични обновления, а за достъпността има само тестове с axe; предложен е `eslint-plugin-jsx-a11y`. Собственикът е одобрил пакета и `dependabot.yml` (2026-11-12).
- Проверка на `eslint-plugin-jsx-a11y` преди инсталиране (CLAUDE.md, правило 13): лиценз MIT; последна версия 6.10.2 от 2024-10-26 (над две години), `peerDependencies` до ESLint 9, а проектът е на ESLint 10, затова `npm install` пада с `ERESOLVE`; без собствени типове за TypeScript; +1,8 хиляди реда в `package-lock.json`. Опит с `overrides` (`eslint: $eslint`) и `flatConfigs.recommended` дава два TypeScript грешки в `eslint.config.js` (липсват типове). Пакетът **не е инсталиран**; решението е на собственика.
- `.github/` е ниво „Първо пита“ (CI и среда); добавянето на `dependabot.yml` е одобрено.

## Критерии за приемане

- `@testing-library/user-event` е в `devDependencies` на `src/web`; `package-lock.json` е от `npm`.
- Поне три теста (по един в `storefront`, `admin`) ползват `userEvent` вместо `fireEvent`, без отслабване на проверките; [testing.md](../handbook/testing.md) казва, че нови тестове са с `userEvent`.
- `.github/dependabot.yml` е за `npm` (`/src/web`), `nuget` (`/`), `github-actions` (`/`) и `docker` (`/src/backend`, `/src/web/docker`), седмично, с групи за малки версии и префикс `build(deps)`.
- D28 е обновен, не е платен до край; D51 записва локалния проблем с тестовете (платен със задача 16j).
- `eslint-plugin-jsx-a11y`: резултатът от проверката е записан (по-горе); решението е отворен въпрос.
- `lint`, `format:check`, тестовете и `build` минават; CI е зелен.

## Граници

- Нов пакет: само `@testing-library/user-event`. Не се инсталира `eslint-plugin-jsx-a11y`.
- Без промяна на `Dockerfile`, workflow файловете и nginx; без преминаване на всички тестове към `userEvent` (постепенно, D28).
- **Дълг:** добавя D51 (приет); променя D28.

## Как се проверява

- От `src/web`: `npm run lint`, `npm run format:check`, тестовете на всеки пакет (`npm run test -w @storefront/<пакет>`), `npm run build`. Backend не се пипа.
- Файлът `dependabot.yml` се проверява от GitHub след сливане (раздел Insights → Dependency graph → Dependabot); до тогава проверката е само на формата с Prettier.
