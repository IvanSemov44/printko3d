## Задача

Спри прекъсващите падащи тестове и различния размер на build-а на Windows, като `@storefront/shared` се чете от `shared/src/index.ts` през `resolve.alias` в двата `vite.config.ts`. Без промяна на поведението.

## Контекст

- Дълг D51 и решение [0026](../decisions/0026-shared-alias-for-windows-links.md): връзката `node_modules/@storefront/shared` сочи към `c:\…` (малка буква), а Vite работи с `C:\…`, затова React и Vitest се зареждат два пъти.
- Симптоми: пет теста в `admin/src/App.test.tsx` (`Cannot read properties of null (reading 'useState')`), `Invalid Chai property` при `npm test` от корена и магазин от 334,2 KB вместо 326,2 KB. В CI не се проявява.

## Критерии за приемане

- `resolve.alias` за `@storefront/shared` е в `storefront/vite.config.ts` и `admin/vite.config.ts`.
- `npm test` от `src/web` минава на Windows няколко пъти подред и след изтриване на кеша; `npm run build` на магазина дава 326,2 KB (хеш `Bt7XALwy`), както CI.
- Решение 0026 е записано и в списъка; D51 е в „Платени“.
- `lint`, `format:check`, `build` и двата Docker образа минават; CI е зелен.

## Граници

- Само двата `vite.config.ts` и документите. Без нови пакети, без `Dockerfile`, CI и nginx.
- **Дълг:** маха D51; не добавя.

## Как се проверява

От `src/web`: `npm run lint`, `npm run format:check`, `npm test` (три пъти), `npm run build`; `docker build` на двата образа.
