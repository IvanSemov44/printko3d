## Задача

Премести общите части на магазина от функционалностите в `src/web/storefront/src/components/`, така че функционалностите да не внасят една от друга (дълг D58), и допълни таблицата „Видове файлове“ във `frontend.md` с компонентните файлове, `*Context`, `*Provider` и `*Host` (дълг D59).

## Контекст

- Дълг: [D58](../tech-debt.md) (нарушава F2 и F4), [D59](../tech-debt.md) (F1). Правила: [frontend.md](../handbook/frontend.md), [refactoring.md](../handbook/refactoring.md) (R7, R8).
- Днешните внасяния между функционалности (от `grep` в прегледа на дълга):
  - `cart` → `product/ImagePlaceholder`, `catalog/availability`, `catalog/CatalogError`, `layout/LoadingStatus`;
  - `catalog` → `product/ImagePlaceholder`, `layout/LoadingStatus`;
  - `product` → `catalog/availability`, `catalog/CatalogError`, `home/NotFoundPage`, `layout/LoadingStatus`, `cart/AddToCartButton`;
  - `home` → `catalog/useCategoriesQuery`, `catalog/ProductCard`; `layout` → `catalog/useCategoriesQuery`, `cart/*`; `seo` → `catalog/availability`, `product/variantSelection`.
- Какво е „общо“ и отива в `components/` (или `lib/`, ако не е компонент): `ImagePlaceholder`, `LoadingStatus`, `CatalogError`, `NotFoundPage`, `availability` (и `leadTime`, ако стои до него). Останалите връзки (например `layout` → `cart/CartButton`, `product` → `cart/AddToCartButton`) са сглобяване на страница и остават през публичния вход на функционалността; плана казва кои.
- Решение за промяна на правилото (F1) е „първо пита“ (CLAUDE.md): таблицата не се променя без одобрен кратък запис в `docs/decisions`.

## Критерии за приемане

- Нито една функционалност не внася от друга освен през публичния ѝ вход, който е описан в плана (да или не, проверява се с `grep` по `from '../`).
- Преместените файлове са с `git mv` (историята се пази); внасянията и тестовете са обновени; тестовете са същите, без промяна на очакванията.
- Поведението не се променя: нито една проверка в тест не е отслабена или махната (R7, R8).
- `frontend.md` познава видовете файлове, които магазинът ползва; има запис в `docs/decisions` (следващ свободен номер), а D58 и D59 са в „Платени“.
- `architecture.md` и картата съвпадат с `ls` на `features/` и `components/`.

## Граници

- Не се променя външният вид и не се пипа админ панелът (D25, D26 са отделно).
- Не се добавя нов код освен преместване и преименуване на внасяния. Нови пакети: не.
- Над пет файла е неизбежно (преместване и внасяния): планът го казва и иска одобрение. Ако излезе голямо, разделя се на 21d-1 (преместване) и 21d-2 (документи).
- Какъв дълг маха? D58, D59. Добавя: не.

## Как се проверява

- Frontend (от `src/web`, от път с главна буква на устройството, виж D57): `npm run format:check`, `npm run lint`, `npm test`, `npm run build`.
- Backend не се пипа; `dotnet build` е достатъчен за проверка, че нищо не е счупено.
- Ръчно: `docker compose up --build`; каталог, продукт, количка и 404 страница се виждат както преди.

## Преди да пишеш код

Дай кратък план: списък на файловете, които се местят, и на връзките, които остават; предположения за мястото на `availability` и `NotFoundPage`.
