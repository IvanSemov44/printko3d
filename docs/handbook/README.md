# Наръчник за инженерната работа

Как се добавя функционалност, как се рефакторира, кога се пишат тестовете, кои са капаните и как се поддържа редът. Наръчникът не повтаря други документи, а сочи към тях. Всяко правило е с отговор „да“ или „не“ и има образец от репото.

**Ред на предимство при конфликт:** [CLAUDE.md](../../CLAUDE.md) → решенията в [docs/decisions](../decisions/README.md) → наръчникът → книгата на Code Maze ([0008](../decisions/0008-code-style-reference-book.md)).

## Кой файл кога се чете

| Правя…                                             | Чета                                                                                                                                                         |
| -------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Нова функционалност през целия стек                | [new-feature.md](new-feature.md), после файла за нивото                                                                                                      |
| Код в `Application` или `Infrastructure`, миграция | [backend.md](backend.md) (къде и колко голямо), [architecture-and-data-rules.md](architecture-and-data-rules.md) (решения, граници, миграции, индекси, seed) |
| Код в `src/web` (екран, форма, hook, заявка)       | [frontend.md](frontend.md) (къде и колко голямо), [react-golden-rules.md](react-golden-rules.md) (как изглежда)                                              |
| Контролер, request, грешка на API                  | [0017](../decisions/0017-api-structure-and-style.md)                                                                                                         |
| Пиша или планирам тест                             | [testing.md](testing.md)                                                                                                                                     |
| Чистя код, делям голям файл                        | [refactoring.md](refactoring.md)                                                                                                                             |
| Пиша на непознато място                            | [pitfalls.md](pitfalls.md)                                                                                                                                   |
| Преглеждам промяна или отварям pull request        | [code-review.md](code-review.md), шаблонът [`.github/pull_request_template.md`](../../.github/pull_request_template.md)                                      |
| Сигурност, Docker, среди, CI                       | [security.md](security.md), [environments.md](environments.md)                                                                                               |
| Планирам поддръжка, преглед на дълга               | [cadence.md](cadence.md)                                                                                                                                     |
| Търся къде стои нещо                               | [architecture.md](../architecture.md)                                                                                                                        |
| Записвам дълг                                      | [tech-debt.md](../tech-debt.md)                                                                                                                              |

## Файлове

| Файл                                                             | Съдържание                                                                                                                                                                                                  |
| ---------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [new-feature.md](new-feature.md)                                 | Ред на работа за функционалност: договор → модел → правило с тест първо → адрес → типове → екран → документи                                                                                                |
| [backend.md](backend.md)                                         | Правила B1–B28: услуги, входни модели и DTO, достъп до данни, миграции                                                                                                                                      |
| [frontend.md](frontend.md)                                       | Правила F1–F24: видове файлове, размер, данни, форми, текстове, достъпност                                                                                                                                  |
| [react-golden-rules.md](react-golden-rules.md)                   | Правила RC1–RC30: компонент, състояние, `useEffect`, hooks, props, TypeScript, именуване, списъци, зареждане и грешка, с лош и добър пример                                                                 |
| [csharp-golden-rules.md](csharp-golden-rules.md)                 | Правила CC1–CC27: услуга, repository, именуване, `async`, `null`, изключения, данни, логове, коментари, с лош и добър пример                                                                                |
| [test-writing-rules.md](test-writing-rules.md)                   | Правила TW1–TW21: име, подредба, независимост, frontend и backend тестове, полезност на теста                                                                                                               |
| [architecture-and-data-rules.md](architecture-and-data-rules.md) | Правила AD1–AD29: решения, граници между проекти, имена в базата, миграции на стъпки, индекси, версия на реда, seed, генерирана схема                                                                       |
| [security.md](security.md)                                       | Правилата за сигурност, преместени от `CLAUDE.md` ([0021](../decisions/0021-claude-md-handbook-and-hooks.md))                                                                                               |
| [environments.md](environments.md)                               | Среди, Docker, render.com и CI, преместени от `CLAUDE.md`                                                                                                                                                   |
| [code-review.md](code-review.md)                                 | Два прохода (агент, човек), размер, нива на забележките, чеклист по област, `/code-review` и `/security-review`, преглед при един разработчик ([0022](../decisions/0022-code-review-with-one-developer.md)) |
| [testing.md](testing.md)                                         | Правила T1–T6: кога се пише тестът, къде стои, помощници, делене на голям файл                                                                                                                              |
| [refactoring.md](refactoring.md)                                 | Правила R1–R17: кога, как и кога не                                                                                                                                                                         |
| [pitfalls.md](pitfalls.md)                                       | Каталог на капаните: „как се познава → защо → как се прави → образец“                                                                                                                                       |
| [cadence.md](cadence.md)                                         | Ритъм: всяка промяна, всяка задача, на пет задачи, месечно, преди етап                                                                                                                                      |

Златните правила за код (16d) се добавят по части; готови са [react-golden-rules.md](react-golden-rules.md) (RC1–RC30: компонент, състояние, `useEffect`, hooks, props, TypeScript, именуване, списъци, зареждане и грешка) , [csharp-golden-rules.md](csharp-golden-rules.md) (CC1–CC27: услуга, repository, именуване, `async`, `null`, изключения, данни, логове, коментари) и [test-writing-rules.md](test-writing-rules.md) (TW1–TW21: име, подредба, независимост, тестове на frontend и backend, полезност на теста). Готови са и [architecture-and-data-rules.md](architecture-and-data-rules.md) (AD1–AD29: решения, граници между проекти, имена в базата, безопасни миграции, индекси, версия на реда, seed, генерирана схема). Готов е и преглед на код с шаблон за pull request и `CODEOWNERS` ([code-review.md](code-review.md), 16e). Готов е и кратък `CLAUDE.md` с решение [0021](../decisions/0021-claude-md-handbook-and-hooks.md) (16b-1). Готови са и настройката за ИИ в `.claude/` (hooks, skills `new-task`, `add-admin-endpoint`, `add-admin-screen`, `refactor`, `debt-review` и агентът `reviewer`; 16b-2, 16b-3). Следват автоматични проверки (16c).

## Граници, на които наръчникът се позовава

| Какво                     | Граница                         | Правило                      |
| ------------------------- | ------------------------------- | ---------------------------- |
| Файл на услуга            | 300 реда или 12 публични метода | B2                           |
| Клас със заявки           | 300 реда                        | B17                          |
| Файл с компонент (`.tsx`) | 250 реда                        | F5                           |
| Тестов файл               | 500 реда                        | F7, [testing.md](testing.md) |

Над границата е ред в [tech-debt.md](../tech-debt.md), а не грешка в CI, докато днешните изключения не бъдат изчистени. Автоматична проверка идва в 16c.

## Как се променя наръчникът

- Правило, което не може да се провери с „да“ или „не“, се пренаписва или се маха.
- Правило, което днешният код нарушава, остава в наръчника и става ред в [tech-debt.md](../tech-debt.md); не се омекотява, за да „мине“.
- Правило, което променя подход, първо става решение в `docs/decisions`.
- `CLAUDE.md` се променя само със задача 16b и решение `0021`.

## Източници

Правилата стъпват на официалната документация, а не на мнение. Връзките са във всеки файл, при правилата, които ги ползват.

- Microsoft Learn: [EF Core](https://learn.microsoft.com/en-us/ef/core/), [асинхронно програмиране](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/task-asynchronous-programming-model), [тестове](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices).
- [react.dev](https://react.dev) и [TanStack Query](https://tanstack.com/query/latest).
- [Testing Library](https://testing-library.com/docs/guiding-principles/).
- Martin Fowler: [рефакториране](https://martinfowler.com/bliki/OpportunisticRefactoring.html), [технически дълг](https://martinfowler.com/bliki/TechnicalDebt.html).
- „Ultimate ASP.NET Core Web API“ (Code Maze), през [0008](../decisions/0008-code-style-reference-book.md).
