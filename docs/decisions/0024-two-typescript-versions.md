# 24. TypeScript 7 за build на приложенията, 5.9 за linter-а

Дата: 2026-11-12
Статус: прието

## Контекст

`storefront` и `admin` ползват TypeScript 7 (`~7.0.2`) за `tsc -b` в build-а. `typescript-eslint` 8 поддържа само `typescript >=4.8.4 <6.1.0`, затова правилата с типове (`strictTypeChecked`) не могат да работят на TypeScript 7. До сега в корена на `src/web` TypeScript 5.9 беше само скрита, транзитивна зависимост.

## Решение

- В `src/web/package.json` е записан `typescript ~5.9.3` като явна зависимост за разработка. Ползват го `typescript-eslint`, ESLint и `openapi-typescript`.
- Приложенията (`storefront`, `admin`) остават на `~7.0.2`.
- Общите настройки на компилатора са в `src/web/shared/tsconfig.base.json` (в `shared`, защото Docker образът копира само `shared` и приложението, без `Dockerfile` да се променя), а приложенията и `shared` го разширяват. Така и двете версии четат едни и същи правила.
- Когато `typescript-eslint` започне да поддържа TypeScript 7, коренът минава на 7 и се маха този документ (нов ред в решенията).

## Последствия

- Linter и build може да не са съгласни за граничен случай, защото проверяват с различни компилатори. Затова CI пуска и двете (`npm run lint` и `npm run build`) и разликата се вижда веднага.
- Две версии на TypeScript в `node_modules` струват място и време за инсталиране, не поведение.
- Редът за преглед: при обновяване на `typescript-eslint` се гледа дали поддържа TypeScript 7 (дълг в `docs/tech-debt.md`).
