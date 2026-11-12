## Задача

Подреди инструментите на React проектите: малки обновления, еднакви настройки на TypeScript на едно място и по-строга проверка на достъпа до масиви. Без нови пакети и без промяна на поведението.

## Контекст

- Прегледът на `src/web` (сесия от 2026-11-12) намери: `@types/node` 24, а средата е Node 26 (`engines`, `.nvmrc`, Docker `node:26.13.0`); малки версии зад последните (`vite`, `@vitejs/plugin-react`, `prettier`, `typescript-eslint`); три почти еднакви `tsconfig` в `storefront`, `admin` и `shared`; няма `noUncheckedIndexedAccess`; няма `.npmrc`; TypeScript 5.9 е само транзитивна зависимост в корена, а приложенията са на 7.0.2.
- `typescript-eslint` 8 поддържа `typescript <6.1.0`, затова линтерът работи на 5.9, а build-ът на 7. Записано в решение [0024](../decisions/0024-two-typescript-versions.md).
- Тази задача е първа от четири преди задача 17 (виж „Преди 17“ в [project-start.md](../project-start.md)): 16g → 16i (админ със зареждане по маршрут) → 16f (React Compiler) → 16h (линтер за достъпност, `userEvent`, `dependabot.yml`).
- `CLAUDE.md`: TypeScript strict; нов пакет само с одобрение; `Dockerfile`, CI и среда са „Първо пита“.

## Критерии за приемане

- Малките версии са обновени в обхвата на `^` (`vite`, `@vitejs/plugin-react`, `prettier`, `typescript-eslint`). `@types/node` е `^26`. `jsdom` остава на 30 (31 е нова голяма версия).
- В `src/web/shared/tsconfig.base.json` са общите настройки (`target`, `strict`, `noUncheckedIndexedAccess`, `noUnused*`, `erasableSyntaxOnly`, `verbatimModuleSyntax`, `noEmit`, `skipLibCheck`). `storefront`, `admin` и `shared` го разширяват и държат само това, което е тяхно (`lib`, `types`, `jsx`, `module`, `tsBuildInfoFile`).
- `noUncheckedIndexedAccess` е включен и кодът минава без `!` и без `as`; тестовете, които четат елемент от масив, проверяват, че го има.
- `typescript ~5.9.3` е записан в `devDependencies` на корена (явно, не по случайност), а приложенията остават на `~7.0.2`. Решение 0024 казва защо и кога се обединяват.
- `src/web/.npmrc` със `engine-strict=true`.
- Нови пакети няма; `package-lock.json` е генериран с `npm`, не на ръка.
- `lint`, `format:check`, `test` и `build` минават без нови предупреждения; размерът на JS и CSS е същият като преди (313,4 KB и 392,2 KB).

## Граници

- Само `src/web` (конфигурации, `package.json`, `package-lock.json`, тестовете, които трябва да се поправят заради `noUncheckedIndexedAccess`) и документите за решението.
- Нови пакети: **не**. Без промяна на `Dockerfile`, CI, nginx, `docker-compose.yml`, `render.yaml`. Без промяна на кода на приложенията освен нужното за `noUncheckedIndexedAccess`.
- Не се обединява TypeScript на една версия (невъзможно, докато `typescript-eslint` не поддържа 7).
- **Дълг.** Добавя един ред в [tech-debt.md](../tech-debt.md): `typescript-eslint` не поддържа TypeScript 7 (две версии в корена); преглед при всяко обновяване на `typescript-eslint`. Не маха дълг.

## Как се проверява

- От `src/web`: `npm ci`, `npm run lint`, `npm run format:check`, `npm test`, `npm run build`. Backend не се пипа.
- След промяна на пакети Vitest може да пази кеш и първото пускане на `npm test` да падне с `Invalid Chai property`; изтрий `node_modules/.vitest-cache` и `node_modules/.vite` (и във всеки пакет) и пусни пак. В CI след `npm ci` кеш няма.
- Тестове: нови не са нужни, а поправените не са отслабени. Преди приемане провери, че `noUncheckedIndexedAccess` е активен (върни `array[0]` без проверка в тест и `tsc` пропада).

## Преди да пишеш код

Дай кратък план и списък с предположенията си. Предположения: обновяването е само в обхвата на `^` (`npm update`); `@types/node` е по-висока версия, без нужда от промяна на кода; седемте-осемте грешки от `noUncheckedIndexedAccess` са само в тестове.
