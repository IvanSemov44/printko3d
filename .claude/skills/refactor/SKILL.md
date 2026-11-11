---
name: refactor
description: Refactor code without changing behaviour (split a large file, extract a third repetition, pay a debt row). Use when asked to clean up, split or restructure existing code. Follows docs/handbook/refactoring.md.
---

# Рефакториране

Правилата са в `docs/handbook/refactoring.md` (R1–R17). Прочети го, преди да започнеш; тук няма правила.

1. Реши дали е рефакториране: ако поведението или договорът на API се променя, не е (R9), и върви по `new-feature.md`. Ако подходът се сменя, първо е нужно решение в `docs/decisions` (R14).
2. Намери реда в `docs/tech-debt.md`, който плащаш, или запиши повода. Остани във файловете на задачата (R13).
3. Дай кратък план и предположения. Над пет файла или смяна на подход: спри и иска одобрение.
4. Започни от зелено: пусни build и тестовете от `CLAUDE.md`, раздел „Команди“ (R6). Липсва ли тест за мястото, първо се пише той.
5. Малки стъпки; преместването е отделна стъпка от промяната на съдържанието (R10, R11). Тестовете не се променят (R8). `refactor:` е отделен commit (R7).
6. След края: build и тестовете са зелени, `git diff` на `openapi.json` и `schema.d.ts` е празен (R9), картата в `docs/architecture.md` е обновена, а редът в дълга е преместен в „Платени“ (R12).

Кажи кои проверки си пускал и какъв е резултатът.
