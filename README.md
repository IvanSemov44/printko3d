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

## Демо в render.com

Демото е с фалшиви данни, без реални плащания, на безплатния план. Услугите (база, API, магазин, админ панел) са описани в [`render.yaml`](render.yaml). Внедряването е от клона `main`, когато проверките на GitHub са зелени. Решението за ръчните стъпки по-долу е [0012](docs/decisions/0012-demo-migrations-and-seed-run-by-hand.md).

**Първо внедряване**

1. В render.com: New → Blueprint, избери репото и клона `main`. Render създава `printko3d-db`, `printko3d-api`, `printko3d-storefront` и `printko3d-admin`.
2. Render пита за четири стойности, които не могат да са в репото, защото адресът на една услуга не е известен на друга. Попълни ги с публичните адреси на услугите (виж ги в таблото; ако имената са свободни, са от вида `https://printko3d-api.onrender.com`):
   - `API_URL` на магазина и на админ панела: адресът на API;
   - `Cors__AllowedOrigins__0` на API: адресът на магазина;
   - `Cors__AllowedOrigins__1` на API: адресът на админ панела.

   Ако имената са заети и render.com добави наставка, или грешиш адрес, поправи стойностите в Environment на услугата. Промяната на `API_URL` изисква ново внедряване.
3. Приложи миграциите и зареди демо данните от компютъра си (нужни са .NET SDK и външният адрес на базата: `printko3d-db` → Connections → External Database URL). Адресът съдържа паролата, затова не го записвай в репото и не го пращай никъде.

   ```bash
   export Database__Url="<External Database URL>"
   dotnet run --no-launch-profile --project src/backend/Storefront.Api -- migrate
   ASPNETCORE_ENVIRONMENT=Demo dotnet run --no-launch-profile --project src/backend/Storefront.Api -- seed
   ```

   Двете команди са безопасни при повторно пускане. `migrate` се пуска и след всяка нова миграция, защото API не я прилага сам.
4. Отвори адреса на магазина: началната страница, категория и продукт, цени в евро и лента „демо версия“.

**Какво да знаеш за безплатния план** (по документацията на render.com към 2026-11-08; провери я пак преди да разчиташ на нея)

- Услугите заспиват след 15 минути без заявки, а първата заявка след това отнема около минута. Работните часове са 750 на месец за целия акаунт.
- Безплатната база изтича след 30 дни (има още 14 дни гратисен период) и е до 1 GB. За ново демо създай нова база и пусни `migrate` и `seed`.
- Базата е достъпна и отвън с паролата си. Данните са фалшиви, но ако искаш, ограничи достъпа до своя адрес с `ipAllowList` на базата.
- Безплатният план няма стъпка преди внедряване (`preDeployCommand`), затова миграциите са ръчни.

**Първоначалната проверка след внедряване** е ръчна, от човек: дълбоките адреси (`/categories/<слъг>`, `/products/<слъг>`) се презареждат без грешка, а конзолата на браузъра е без грешки за CORS и CSP. Колко точно трае първата заявка след заспиване, запиши тук след първата проверка: _още не е измерено_.

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

`render.yaml` в корена описва демото в render.com. Останалите файлове се добавят със съответните задачи.
