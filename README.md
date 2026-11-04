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
