## Задача
Подреди `Storefront.Api` по правилата на решение 0017 и плати дълговете D1 до D8 от `docs/tech-debt.md`, без да променяш поведението на API.

## Контекст
- Решение 0017 (правила 1 до 11), `docs/architecture.md` (целева подредба) и `docs/tech-debt.md` (D1 до D8). Описанието на задачата 11c е в `docs/tasks/11c-code-rules-and-debt-register.md`.
- Одобрени отговори: `ActionResult<T>` с `Ok(...)` навсякъде, `PUT` връща `200` с тяло, `/health` става `HealthController`, мапирането е явно с `ToModel()` без нов пакет.

## Критерии за приемане
- `Program.cs` е кратък (само команда, `AddApiServices`, `UseApiPipeline`, `Run`); в корена на проекта няма други `.cs` файлове.
- Папки `Commands/`, `Auth/`, `Errors/`, `Requests/`, `Setup/`; request моделите са по един на файл.
- Всички действия връщат `ActionResult<T>` или `ActionResult`; няма minimal-API адреси; `ProblemDetails` се сглобява само в `Errors/ApiProblems.cs`.
- Всяко действие има XML коментар с кодовете на грешките.
- Договорът на API се променя само така: тагът на `/health` е `Health` (операцията `GetHealth` вече няма име), а входът и други адреси описват повече кодове в OpenAPI. `openapi.json` и типовете са обновени.
- Тялото на `401` за грешен вход получава `traceId`, като другите грешки. Тестът за „едно и също съобщение“ сравнява всичко без `traceId`.

## Граници
- Без промяна на поведението, на схемата, на Dockerfile, CI, `render.yaml`, на `CLAUDE.md` и на публичните отговори освен горното.
- Без нови пакети и без архитектурни тестове (дълг D14).
- Само `Storefront.Api`, плюс едно ново изключение в `Application` (`InvalidCredentialsException`).

## Как се проверява
- От корена: `dotnet build Storefront.slnx`, `dotnet format Storefront.slnx --verify-no-changes`, `dotnet test Storefront.slnx` (изисква Docker). От `src/web`: `npm run lint`, `npm run format:check`, `npm test`, `npm run build`.
- Правилата 1 до 5 и 8 от 0017 се проверяват с търсене (дължина на `Program.cs`, `ls` на корена, `record` в `Controllers/`, голи типове за връщане, `new ProblemDetails` извън `Errors/`).
- Преди да се приеме тест, се чупи нарочно кодът: кодът на грешен вход и кодът на `503` от `/health`.
- Ръчно: `docker compose up --build`, вход в панела и работа с категории, `GET /health`.

## Преди да пишеш код
Дай кратък план и списък с предположенията си. Ако промяната излиза над пет файла или над 15 минути преглед, спри и поискай одобрение (тук е над пет файла, одобрено с 0017 и отговорите по-горе).
