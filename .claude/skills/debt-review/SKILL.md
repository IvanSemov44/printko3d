---
name: debt-review
description: Run the technical debt review that is due every five tasks. Use when asked to review debt, to prepare the debt task, or after the fifth task since the last review. Produces a report and a proposed plan; pays nothing without approval.
---

# Преглед на дълга

Процедурата е в `docs/handbook/cadence.md`, раздел „Как се прави преглед на всеки пет задачи“ (стъпки 1–7). Следвай я; тук няма правила. Как се записва ред е в раздел „Как се записва дълг“, а какво не е дълг е в „Какво не е дълг“.

1. Работи в режим „само отчет“: прочети `docs/tech-debt.md`, `docs/architecture.md`, `docs/handbook/pitfalls.md` и размерите на файловете (`wc -l`) спрямо границите в `docs/handbook/README.md`, раздел „Граници, на които наръчникът се позовава“.
2. Дай отчет по стъпките на `cadence.md`: добавени и платени редове оттогава; редове с цена **В**; редове с минала дата за преглед; нови файлове над границата; разминаване между картата и `ls`; повтарящи се грешки за `pitfalls.md`.
3. Предложи кои редове да се платят, кои да се приемат с причина и кои да се махнат. За редовете за плащане предложи задачи по `new-task`.
4. Не плащай дълг, не рефакторирай и не променяй `tech-debt.md`, докато човекът не одобри. Решението кой ред се плаща е негово.
