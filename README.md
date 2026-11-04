# Принтко 3Д

Онлайн магазин за 3D принтове. Кодът е общ, а всичко, което е специфично за конкретен магазин (име, валута, цветове, контакти), е настройка в базата, така че проектът да може да се ползва и за друг магазин.

## Състояние

Репото е в начален етап. Налични са само документацията, правилата за код и структурата на папките. Приложението се създава със задачите от [`docs/project-start.md`](docs/project-start.md), като се започне със [`docs/tasks/01-skeleton.md`](docs/tasks/01-skeleton.md).

Докато задача 1 не е изпълнена, `docker compose up` и командите за тестове още не съществуват. Този файл се допълва със стъпките за стартиране в края на задача 1.

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
   └─ e2e/
```

Останалите файлове (`docker-compose.yml`, `render.yaml`, CI, `.env.example`) се добавят със съответните задачи.
