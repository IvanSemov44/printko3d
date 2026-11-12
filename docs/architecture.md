# Архитектура: какво къде стои

Карта за човек и за ИИ: къде слагаш ново нещо и какво не се прави на всяко място. Общата структура на репото е в [project-start.md](project-start.md), правилата за код са в [CLAUDE.md](../CLAUDE.md), а защо са избрани слоевете са в решения [0006](decisions/0006-book-style-layers-and-controllers.md) и [0008](decisions/0008-code-style-reference-book.md). Този файл казва само **къде стои кой код**. Правилата за `Api` са в [0017](decisions/0017-api-structure-and-style.md), правилата за останалото и редът на работа са в [наръчника](handbook/README.md), а известните отклонения са в [tech-debt.md](tech-debt.md).

**Как се чете.** Описано е това, което е в репото. Когато файлът и кодът се разминават, поправя се файлът или се записва дълг в [tech-debt.md](tech-debt.md).

## Backend: четири проекта

Зависимостите вървят навътре: `Api` и `Infrastructure` зависят от `Application`, а `Application` от `Domain`. `Domain` не зависи от нищо.

| Проект                      | Какво е тук                                                                                                       | Какво не се прави тук                                       |
| --------------------------- | ----------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------- |
| `Storefront.Domain`         | Прости класове на обектите (`Category`, `Product`…), общият `Entity` с `Id`, `CreatedAt`, `UpdatedAt`, и enum-ите | Бизнес правила, EF, зависимости                             |
| `Storefront.Application`    | Услуги с правилата на модела, интерфейси на repository-та, входни модели, DTO, изключения                         | Достъп до база (само през интерфейси от `Contracts/`), HTTP |
| `Storefront.Infrastructure` | EF Core (`DbContext`, конфигурации, миграции), repository-та, заявки за четене, Identity, проверка на базата      | Бизнес правила                                              |
| `Storefront.Api`            | Контролери, request модели, обработка на грешки, настройка на вход и конвейера, команди                           | Бизнес правила, директен достъп до `DbContext`              |

### `Storefront.Application`

| Папка                 | Съдържание                                                                                                                                                                                                                                                |
| --------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Catalog/Services/`   | Услуги (`CategoryService`, `ProductService`, `StoreSettingsService`, `CatalogReadService`), техните интерфейси, `IServiceManager` и `ServiceManager`, статичните правила без база (`ProductRules`, `VariantRules`, `SlugRules`…), `Guard` и `FieldErrors` |
| `Catalog/Inputs/`     | Входните модели на услугите: `New…` и `Update…` (`NewCategory`, `UpdateCategory`…)                                                                                                                                                                        |
| `Catalog/Queries/`    | DTO и параметри за четене: това, което излиза от API (`CategoryDto`, `CategoryAdminDto`, `PagedResult<T>`, `ProductListQuery`)                                                                                                                            |
| `Catalog/Images/`     | Правила и граници за снимките на продукти (`ImageRules`, `ImageLimits`)                                                                                                                                                                                   |
| `Contracts/`          | Интерфейсите на repository-та и `IRepositoryManager`, `ICatalogQueries` (четене за магазина и за админа)                                                                                                                                                  |
| `Exceptions/`         | `NotFoundException`, `ValidationFailedException`, `ConflictException`. `ApiExceptionHandler` ги превежда в `ProblemDetails`                                                                                                                               |
| `Auth/`               | Интерфейси и модели за вход на администратор                                                                                                                                                                                                              |
| `Health/`, `Seeding/` | Проверка на базата и демо данните                                                                                                                                                                                                                         |

**Къде слагам ново бизнес правило?** В метод на услугата в `Application` (например `CategoryService`), с unit тест на правилото. Ако правилото може да се наложи и от базата (уникалност, външен ключ), налага се и там.

### `Storefront.Infrastructure`

| Папка           | Съдържание                                                                         |
| --------------- | ---------------------------------------------------------------------------------- |
| `Persistence/`  | `StorefrontDbContext`, `Configurations/` (по един клас на обект), `Migrations/`    |
| `Repositories/` | `*Repository`, `CatalogQueries` (четене към DTO със `Select`), `RepositoryManager` |
| `Identity/`     | ASP.NET Identity: `AdminUser`, услуги за вход и създаване на администратор         |
| `Storage/`      | Хранилище за снимки, съвместимо със S3 (`S3ImageStorage`, решение 0018)            |
| `Health/`       | Проверка на връзката с базата                                                      |

**Къде слагам нова колона?** В обекта на `Domain`, в конфигурацията му, нова миграция с ясно име, и се обновява `docs/database-schema.md` (генерира се, решение 0007).

### `Storefront.Api`

Правилата са в [0017](decisions/0017-api-structure-and-style.md).

| Папка          | Съдържание                                                                                                                                                                                                                                                                                                                   |
| -------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Controllers/` | Публични: `CategoriesController`, `ProductsController`, `StoreSettingsController`, `SitemapController`, `HealthController`. Затворени: `AuthController` (вход), `AdminCategoriesController`, `AdminProductsController`, `AdminProductImagesController`, `AdminStoreSettingsController`. Образец: `AdminCategoriesController` |
| `Requests/`    | Request моделите, един тип на файл, с атрибути за валидация и `ToModel()`. Образец: `UpdateCategoryRequest`                                                                                                                                                                                                                  |
| `Commands/`    | `MigrateCommand`, `SeedCommand`, `CreateAdminCommand` и `ApiCommands` (диспечерът). Образец: `MigrateCommand`                                                                                                                                                                                                                |
| `Auth/`        | `AdminAuthSetup` (вход, бисквитка, затворено по подразбиране, лимит), `AdminLoginOptions`, `CsrfHeaderMiddleware`                                                                                                                                                                                                            |
| `Errors/`      | `ApiExceptionHandler` (изключение към `ProblemDetails`) и `ApiProblems` (единственото място, което решава как изглежда грешка)                                                                                                                                                                                               |
| `Setup/`       | `ApiSetup`: `AddApiServices` и `UseApiPipeline`. `Program.cs` само ги вика                                                                                                                                                                                                                                                   |

**Къде слагам нов адрес?** В контролер, под `/api/v1`. Нов админ адрес е затворен сам (политика „затворено по подразбиране“); не се слага `[AllowAnonymous]`, освен ако е публичен и е добавен нарочно в `ClosedByDefaultTests`.

## Тестове

| Проект                              | Какво е                                                                                 | Къде                                                                                                                    |
| ----------------------------------- | --------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `tests/Storefront.UnitTests`        | Правила на услугите, без база и мрежа. `FakeRepositoryManager` замества repository-тата | един плосък списък, файл на област (`CategoryManagementTests`)                                                          |
| `tests/Storefront.IntegrationTests` | API срещу истинска PostgreSQL в контейнер (Testcontainers)                              | `Auth/` (админ адреси), `Persistence/`, `ReadApi/`, `Storage/`; помощни класове `*ApiHost`, `ApiFactory`, `CatalogData` |
| `tests/e2e/`                        | Playwright. Още празно (D15)                                                            |                                                                                                                         |
| Frontend                            | Vitest и Testing Library, до файла, който тестват (`*.test.ts(x)`)                      |                                                                                                                         |

**Къде слагам тест?** До най-близкия по вид: правило на услуга означава unit тест, договор на адрес или ограничение на базата означава интеграционен тест, поведение на екран означава компонентен тест до компонента.

## Frontend (`src/web`, npm workspaces)

| Пакет         | Съдържание                                                                                                                                                                                                                                                                                                                                |
| ------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `storefront/` | Публичният магазин. `src/features/` по функционалност: `catalog`, `product`, `layout`, `home`, `seo`, `store-settings`. Тестови помощници в `src/testing/`                                                                                                                                                                                |
| `admin/`      | Админ панелът. `src/features/`: `auth`, `categories`, `products`, `settings`. Влиза в `SignedIn` (меню и маршрути). Общи компоненти в `src/components/` (`FormField`, `useFormValues`, `ConfirmDialog`, `PageLoadBoundary`)                                                                                                               |
| `shared/`     | Общ код: API слой в `src/api/` (публичен `client.ts`; админ: `adminHttp.ts`, `adminCategories.ts`, `adminProducts.ts`, `adminSettings.ts`, `auth.ts`; проверка на отговорите `decode.ts`), генерираните типове (`src/api/schema.d.ts`, **не се пише на ръка**) и `openapi.json`, преводач (`i18n.ts`), пари (`money.ts`), цветове, снимки |

Подредбата на файловете във всяка функционалност и границите за размер са във [frontend.md](handbook/frontend.md).

**Къде слагам нов текст за потребителя?** Във файла `locales/bg.json` на съответното приложение, никога в компонента.
**Откъде идват типовете за отговор?** От `openapi.json`: след промяна на API се обновява документът и се пуска `npm run api:types --workspace @storefront/shared`.
**Къде слагам заявка към API?** В `shared/src/api` и се ползва през TanStack Query (решение 0010), не директно от компонента.

## Бърза справка: „Искам да…“

| Искам да…                    | Правя                                                                                                                                       |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| Добавя правило за категория  | Метод в `CategoryService` и тест в `CategoryManagementTests`                                                                                |
| Добавя админ адрес           | Метод в контролер, DTO в `Application/Catalog/Queries`, `ProducesResponseType`, тест за `401` и `403`, обновен `openapi.json` и типове      |
| Променя схемата              | Обект в `Domain`, конфигурация, миграция, обновен `docs/database-schema.md` и интеграционен тест на ограничението                           |
| Добавя екран в панела        | Папка в `admin/src/features/<име>`, текстове в `locales/bg.json`, маршрут в `SignedIn`, тест на поведението                                 |
| Записвам отклонение или дълг | Ред в [tech-debt.md](tech-debt.md); ако променя правило, решение в `docs/decisions`                                                         |
| Правя цяла функционалност    | Ред на работа в [new-feature.md](handbook/new-feature.md)                                                                                   |
| Чистя или деля голям файл    | [refactoring.md](handbook/refactoring.md) и границите за размер във [frontend.md](handbook/frontend.md) и [backend.md](handbook/backend.md) |
| Пиша на непознато място      | Капаните в [pitfalls.md](handbook/pitfalls.md)                                                                                              |
