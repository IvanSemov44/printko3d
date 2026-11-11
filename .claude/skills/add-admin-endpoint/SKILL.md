---
name: add-admin-endpoint
description: Add or change an admin API endpoint (contract, model, service rule with test first, query, controller, OpenAPI types). Use for any backend work behind the admin panel. Follows docs/handbook/new-feature.md steps 1 to 6.
---

# Админ адрес (бекенд)

Редът е в `docs/handbook/new-feature.md`, стъпки 1–6, и „Бързи варианти“ за малка промяна. Следвай го; тук няма правила.

Преди първата стъпка дай кратък план и предположения (`CLAUDE.md`, „Работа с ИИ: граници“). **Спри и питай**, ако трябва нова таблица, промяна на договора на API, нов тип права или нов пакет („Първо пита“).

Къде е правилото за всяка стъпка:

| Стъпка                             | Файл                                                                                              |
| ---------------------------------- | ------------------------------------------------------------------------------------------------- |
| 1 Договор                          | `docs/decisions/0017-api-structure-and-style.md`, кодовете на грешките в `backend.md` (B11–B13)   |
| 2 Модел и миграция                 | `backend.md` (B18–B28), `architecture-and-data-rules.md` (AD)                                     |
| 3 Правило в услугата, тестът първо | `backend.md` (B1–B13), `csharp-golden-rules.md` (CC), `testing.md`, `test-writing-rules.md` (TW)  |
| 4 Четене                           | `backend.md` (B14–B17)                                                                            |
| 5 Адрес                            | `0017`, `new-feature.md` стъпка 5                                                                 |
| 6 Договор към фронтенда            | `new-feature.md` стъпка 6: `npm run api:types` в `src/web/shared`, никога на ръка в `schema.d.ts` |

Проверка: командите от `CLAUDE.md`, раздел „Команди“. Преди да кажеш „готово“, мини през `pitfalls.md`, раздел Backend, и кажи кои проверки си пускал и какъв е резултатът.
