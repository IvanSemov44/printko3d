---
name: add-admin-screen
description: Add or change a screen in the admin panel (API layer, hook, validation, page, form, texts, component test). Use for frontend work in src/web/admin. Follows docs/handbook/new-feature.md steps 7 to 9.
---

# Екран в админ панела

Редът е в `docs/handbook/new-feature.md`, стъпки 7–9 (и 10–11 за документи и проверка). Следвай го; тук няма правила.

Предпоставка: адресът и типовете са готови (стъпки 1–6). Ако не са, **спри** и кажи, че първо трябва `add-admin-endpoint`. Не пиши `schema.d.ts` на ръка.

Преди кода дай кратък план и предположения.

| Какво                                                        | Файл                                                          |
| ------------------------------------------------------------ | ------------------------------------------------------------- |
| Видове файлове, имена, граници за размер                     | `docs/handbook/frontend.md` (F1–F24)                          |
| Как изглежда компонент, hook, състояние, `useEffect`, типове | `docs/handbook/react-golden-rules.md` (RC1–RC30)              |
| Валидация, форми, текстове в `locales/bg.json`, достъпност   | `frontend.md`, `react-golden-rules.md`                        |
| Тест на поведението, `getByRole`, `axe`                      | `docs/handbook/test-writing-rules.md` (TW), `testing.md`      |
| Заявки и ключове                                             | решение `docs/decisions/0010-tanstack-query-for-api-layer.md` |

Покажи зареждане, грешка и празно състояние (RC28). Командите за проверка са в `CLAUDE.md`, раздел „Команди“ (от `src/web`: `npm run lint`, `npm run format:check`, `npm test`). Преди „готово“ мини през `pitfalls.md`, раздел Frontend, и кажи кои проверки си пускал.
