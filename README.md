# Принтко 3Д

Онлайн магазин за 3D принтове. Кодът е общ, а всичко, което е специфично за конкретен магазин (име, валута, цветове, контакти), е настройка в базата, така че проектът да може да се ползва и за друг магазин.

## Как се стартира

Нужни са Docker Desktop, .NET SDK (версията е в `global.json`) и Node.js (версията е в `.nvmrc`).

```bash
cp .env.example .env
docker compose up --build
```

| Услуга | Адрес |
| --- | --- |
| Магазин | http://localhost:5173 |
| Админ панел | http://localhost:5174 |
| API (`GET /health`) | http://localhost:8080/health |
| PostgreSQL | `localhost:5433` |

Магазинът и админ панелът показват дали API е достъпно. Портовете се сменят в `.env` (`API_PORT`, `STOREFRONT_PORT`, `ADMIN_PORT`, `POSTGRES_PORT`). Ако порт е зает от друга програма, например друг Vite сървър, `localhost` в браузъра може да отвори нея вместо контейнера. Тогава я спри или смени порта.

## Демо данни

Командата `seed` зарежда 16 фалшиви продукта в четири категории и един ред настройки на магазина (`is_demo = true`). Данните са едни и същи при всяко зареждане. Командата работи само в среда `Development` и `Demo`, иначе излиза с грешка. Тя **не прилага миграции** и не се пуска сама при старт на API. Ако схемата липсва, спира и казва какво да направиш.

1. Приложи миграциите върху базата (от хоста; PostgreSQL от `docker compose up db` слуша на порт 5433, вземи паролата от `.env`):

   ```bash
   dotnet tool restore
   export Database__ConnectionString="Host=localhost;Port=5433;Database=storefront;Username=storefront;Password=<паролата от .env>"
   dotnet ef database update --project src/backend/Storefront.Infrastructure
   ```

2. Зареди данните, или локално:

   ```bash
   dotnet run --project src/backend/Storefront.Api -- seed
   ```

   или в Docker (услугата `api` е със среда `Development` по подразбиране; за демо средата задай `ASPNETCORE_ENVIRONMENT=Demo`):

   ```bash
   docker compose run --rm api seed
   ```

`dotnet run` ползва профила от `launchSettings.json`, който задава `Development`. За друга среда добави `--no-launch-profile`, например `ASPNETCORE_ENVIRONMENT=Production dotnet run --no-launch-profile --project src/backend/Storefront.Api -- seed` отказва с код 1.

Повторното пускане е безопасно: продуктите и категориите, които вече ги има (по слъг), се пропускат, а настройките не се презаписват. За начисто изтрий базата (`docker compose down -v`), приложи миграциите и пусни `seed` отново.

## API за четене на каталога

Всички адреси са публични `GET` под `/api/v1`. Показват се само `Published` продукти от активни категории и само активните им варианти. Цените са цели числа в най-малката парична единица, а валутата е в `/store-settings`. OpenAPI документът е на `/openapi/v1.json` (само в среда `Development`).

| Адрес | Какво връща |
| --- | --- |
| `/categories` | Активните категории, подредени по `sortOrder` |
| `/products` | Страница с продукти. Параметри: `category` (слъг, обхваща и подкатегориите; непознат слъг дава празна страница), `fulfilmentType` (`InStock` или `MadeToOrder`), `sort` (`name`, `-name`, `price`, `-price`; по подразбиране `name`), `page` (1–10000), `pageSize` (1–48, по подразбиране 12) |
| `/products/{slug}` | Един продукт с описание, варианти и всички снимки; `404`, ако не се вижда в магазина |
| `/store-settings` | Настройките на магазина |

Списъците връщат `{ items, page, pageSize, totalCount, totalPages }`. Невалидни параметри дават `400`, липсващ ресурс `404`, и двата във формат `ProblemDetails` с допълнително поле `code` (`validation.failed`, `product.not_found`, `store_settings.not_found`), по което клиентът избира текста. `/categories` е без страниране (решение 0009). Снимките са само с `storageKey`, не с пълен адрес.

## Тестове и проверки

Backend (интеграционните тестове стартират PostgreSQL с Testcontainers, затова Docker трябва да работи):

```bash
dotnet build Storefront.slnx
dotnet test Storefront.slnx
dotnet format Storefront.slnx --verify-no-changes
```

Frontend (от папка `src/web`):

```bash
npm ci
npm run lint
npm run format:check
npm test
npm run build
```

## CI

При всеки pull request към `main` (и при push в `main`) GitHub Actions пуска `.github/workflows/ci.yml` с пет jobs: `backend`, `frontend`, `docker`, `audit` и `secrets`. Версиите на .NET и Node.js се вземат от `global.json` и `.nvmrc`.

Същите проверки локално (backend и frontend командите са в раздела по-горе):

```bash
docker build -f src/backend/Dockerfile .
docker build -f src/web/docker/Dockerfile --build-arg APP=storefront src/web
docker build -f src/web/docker/Dockerfile --build-arg APP=admin src/web
dotnet list Storefront.slnx package --vulnerable --include-transitive
(cd src/web && npm audit --omit=dev --audit-level=high)
gitleaks git --redact --verbose
```

`audit` блокира при High и Critical. Находките в dev инструментите (умерени, през `typescript-eslint`) само се показват.

## Състояние

Налични са скелетът на backend (Domain, Application, Infrastructure, Api), магазинът и админ панелът, Docker файловете и тестовете за `/health`. Следващите задачи са в [`docs/project-start.md`](docs/project-start.md).

## Документи

| Файл | Съдържание |
| --- | --- |
| [`CLAUDE.md`](CLAUDE.md) | Правила за код и за работа с ИИ. Подава се в началото на всяка сесия с ИИ |
| [`docs/requirements.md`](docs/requirements.md) | Обхват, взети решения и изисквания за версия 1 |
| [`docs/data-model.md`](docs/data-model.md) | Модел на данните на каталога |
| [`docs/project-start.md`](docs/project-start.md) | Структура на репото и задачи по ред |
| [`docs/decisions/`](docs/decisions/) | Записани решения |
| [`docs/tasks/`](docs/tasks/) | Описания на задачите за ИИ |

## Технологии

ASP.NET и EF Core, PostgreSQL, React с TypeScript (SPA с Vite), Docker. Демо средата е в render.com.

## Структура

```
.
├─ CLAUDE.md
├─ README.md
├─ docs/
├─ src/
│  ├─ backend/            Storefront.Domain, .Application, .Infrastructure, .Api
│  └─ web/
│     ├─ storefront/      публичният магазин
│     ├─ admin/           админ панел
│     └─ shared/          общ код
└─ tests/
   ├─ Storefront.UnitTests/
   ├─ Storefront.IntegrationTests/
   └─ e2e/
```

Останалите файлове (`render.yaml`, CI) се добавят със съответните задачи.
