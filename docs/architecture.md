# Архитектура: какво къде стои

Карта за човек и за ИИ: къде слагаш ново нещо и какво не се прави на всяко място. Общата структура на репото е в [project-start.md](project-start.md), правилата за код са в [CLAUDE.md](../CLAUDE.md), а защо са избрани слоевете са в решения [0006](decisions/0006-book-style-layers-and-controllers.md) и [0008](decisions/0008-code-style-reference-book.md). Този файл казва само **къде стои кой код**. Правилата за `Api` са в [0017](decisions/0017-api-structure-and-style.md), а известните отклонения са в [tech-debt.md](tech-debt.md).

**Как се чете.** „Днес“ е това, което е в репото сега. „След 11d“ е целевата подредба на `Api`, която още не е направена. Когато файлът и кодът се разминават, поправя се файлът или се записва дълг.

## Backend: четири проекта

Зависимостите вървят навътре: `Api` и `Infrastructure` зависят от `Application`, а `Application` от `Domain`. `Domain` не зависи от нищо.

| Проект | Какво е тук | Какво не се прави тук |
| --- | --- | --- |
| `Storefront.Domain` | Прости класове на обектите (`Category`, `Product`…), общият `Entity` с `Id`, `CreatedAt`, `UpdatedAt`, и enum-ите | Бизнес правила, EF, зависимости |
| `Storefront.Application` | Услуги с правилата на модела, интерфейси на repository-та, входни модели, DTO, изключения | Достъп до база (само през интерфейси от `Contracts/`), HTTP |
| `Storefront.Infrastructure` | EF Core (`DbContext`, конфигурации, миграции), repository-та, заявки за четене, Identity, проверка на базата | Бизнес правила |
| `Storefront.Api` | Контролери, request модели, обработка на грешки, настройка на вход и конвейера, команди | Бизнес правила, директен достъп до `DbContext` |

### `Storefront.Application`

| Папка | Съдържание |
| --- | --- |
| `Catalog/` | Услуги (`CategoryService`, `ProductService`, `StoreSettingsService`), техните интерфейси, входните модели (`NewCategory`, `UpdateCategory`…), `IServiceManager` и `ServiceManager`. Претрупана (D13) |
| `Catalog/Queries/` | DTO и параметри за четене: това, което излиза от API (`CategoryDto`, `CategoryAdminDto`, `PagedResult<T>`, `ProductListQuery`) |
| `Contracts/` | Интерфейсите на repository-та и `IRepositoryManager`, `ICatalogQueries` (четене за магазина и за админа) |
| `Exceptions/` | `NotFoundException`, `ValidationFailedException`, `ConflictException`. `ApiExceptionHandler` ги превежда в `ProblemDetails` |
| `Auth/` | Интерфейси и модели за вход на администратор |
| `Health/`, `Seeding/` | Проверка на базата и демо данните |

**Къде слагам ново бизнес правило?** В метод на услугата в `Application` (например `CategoryService`), с unit тест на правилото. Ако правилото може да се наложи и от базата (уникалност, външен ключ), налага се и там.

### `Storefront.Infrastructure`

| Папка | Съдържание |
| --- | --- |
| `Persistence/` | `StorefrontDbContext`, `Configurations/` (по един клас на обект), `Migrations/` |
| `Repositories/` | `*Repository`, `CatalogQueries` (четене към DTO със `Select`), `RepositoryManager` |
| `Identity/` | ASP.NET Identity: `AdminUser`, услуги за вход и създаване на администратор |
| `Health/` | Проверка на връзката с базата |

**Къде слагам нова колона?** В обекта на `Domain`, в конфигурацията му, нова миграция с ясно име, и се обновява `docs/database-schema.md` (генерира се, решение 0007).

### `Storefront.Api`

Правилата са в [0017](decisions/0017-api-structure-and-style.md).

| Папка | Днес | След 11d |
| --- | --- | --- |
| `Controllers/` | `AuthController`, `CategoriesController`, `AdminCategoriesController`, `ProductsController`, `StoreSettingsController` | същото, плюс `HealthController` |
| `Requests/` | няма (request моделите са в контролерите) | по един record на файл |
| `Commands/` | няма (в корена са `MigrateCommand`, `SeedCommand`, `CreateAdminCommand`) | трите команди |
| `Auth/` | няма (в корена са `AdminAuthSetup`, `AdminLoginOptions`) | настройка на вход, настройки, CSRF проверка |
| `Errors/` | няма (`ApiExceptionHandler` е в корена) | `ApiExceptionHandler` и фабриката на `ProblemDetails` |
| `Setup/` | няма (всичко е в `Program.cs`) | `AddApi…` и `UseApi…` |

**Къде слагам нов адрес?** В контролер, под `/api/v1`. Нов админ адрес е затворен сам (политика „затворено по подразбиране“); не се слага `[AllowAnonymous]`, освен ако е публичен и е добавен нарочно в `ClosedByDefaultTests`.

## Тестове

| Проект | Какво е | Къде |
| --- | --- | --- |
| `tests/Storefront.UnitTests` | Правила на услугите, без база и мрежа. `FakeRepositoryManager` замества repository-тата | един плосък списък, файл на област (`CategoryManagementTests`) |
| `tests/Storefront.IntegrationTests` | API срещу истинска PostgreSQL в контейнер (Testcontainers) | `Auth/`, `Persistence/`, `ReadApi/`; помощни класове `*ApiHost`, `ApiFactory`, `CatalogData` |
| `tests/e2e/` | Playwright. Още празно (D15) | |
| Frontend | Vitest и Testing Library, до файла, който тестват (`*.test.ts(x)`) | |

**Къде слагам тест?** До най-близкия по вид: правило на услуга означава unit тест, договор на адрес или ограничение на базата означава интеграционен тест, поведение на екран означава компонентен тест до компонента.

## Frontend (`src/web`, npm workspaces)

| Пакет | Съдържание |
| --- | --- |
| `storefront/` | Публичният магазин. `src/features/` по функционалност: `catalog`, `product`, `layout`, `home`, `store-settings`. Тестови помощници в `src/testing/` |
| `admin/` | Админ панелът. `src/features/`: `auth`, `categories`. Влиза в `SignedIn` (меню и маршрути) |
| `shared/` | Общ код: API клиент (`src/api/client.ts`, `auth.ts`, `adminCategories.ts`), генерираните типове (`src/api/schema.d.ts`, **не се пише на ръка**) и `openapi.json`, преводач (`i18n.ts`), пари, цветове, снимки |

**Къде слагам нов текст за потребителя?** Във файла `locales/bg.json` на съответното приложение, никога в компонента.
**Откъде идват типовете за отговор?** От `openapi.json`: след промяна на API се обновява документът и се пуска `npm run api:types --workspace @storefront/shared`.
**Къде слагам заявка към API?** В `shared/src/api` и се ползва през TanStack Query (решение 0010), не директно от компонента.

## Бърза справка: „Искам да…“

| Искам да… | Правя |
| --- | --- |
| Добавя правило за категория | Метод в `CategoryService` и тест в `CategoryManagementTests` |
| Добавя админ адрес | Метод в контролер, DTO в `Application/Catalog/Queries`, `ProducesResponseType`, тест за `401` и `403`, обновен `openapi.json` и типове |
| Променя схемата | Обект в `Domain`, конфигурация, миграция, обновен `docs/database-schema.md` и интеграционен тест на ограничението |
| Добавя екран в панела | Папка в `admin/src/features/<име>`, текстове в `locales/bg.json`, маршрут в `SignedIn`, тест на поведението |
| Записвам отклонение или дълг | Ред в [tech-debt.md](tech-debt.md); ако променя правило, решение в `docs/decisions` |
