# Златни правила: архитектура и данни

Кога се записва решение, къде са границите между проектите и как се пази базата: имена, миграции, индекси, версия на реда, демо данни и схема. [architecture.md](../architecture.md) е картата (къде стои кой код), [backend.md](backend.md) са правилата B1–B28 за слоевете, а този файл казва **кога и как се променя самата подредба и схемата**. Правилата не повтарят другите документи, а ги допълват и сочат към тях (таблицата в края показва връзките).

**Формат.** Всяко правило е в една рамка: **правило** (отговор „да“ или „не“) → **лош пример** → **добър пример** → **образец** от репото или от документацията. Редът „Днес“ казва дали кодът го следва; „не“ е ред в [tech-debt.md](../tech-debt.md). Номерата са `AD1`, `AD2`… (architecture and data), за да не се бъркат с `B`, `CC`, `TW`, `RC`.

**Версии.** PostgreSQL 17 (образът в тестовете е `postgres:17.6-alpine`), EF Core 10, Npgsql 10.

**Източници.** Решенията [0006](../decisions/0006-book-style-layers-and-controllers.md), [0007](../decisions/0007-generated-database-schema-document.md), [0012](../decisions/0012-demo-migrations-and-seed-run-by-hand.md), [0015](../decisions/0015-data-protection-keys-in-database.md), [0016](../decisions/0016-admin-categories-list-and-row-version.md), [0019](../decisions/0019-store-settings-row-version-and-currency-lock.md). EF Core: [Migrations overview](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/), [Customize migration code](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing), [Applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying), [Concurrency tokens](https://learn.microsoft.com/en-us/ef/core/saving/concurrency). Npgsql: [Concurrency tokens и `xmin`](https://www.npgsql.org/efcore/modeling/concurrency.html). PostgreSQL: [Indexes](https://www.postgresql.org/docs/17/indexes.html), [Using EXPLAIN](https://www.postgresql.org/docs/17/using-explain.html).

**Какво не е тук.** Именуване на таблици и индекси по подразбиране (B20), конфигурация на обект (B18–B19), заявки и страниране (B14–B17), брой и име на миграция (B22–B25, B27), обновяване на схемата (B28), `async`, `null` и изключения ([csharp-golden-rules.md](csharp-golden-rules.md)). Тук не се повтарят. Автоматичните проверки за границите са в 16c.

**Как се налага.** Най-вече при преглед. Автоматично: `Directory.Build.props` (предупреждения като грешки), `MigrateCommandTests`, `MigrationAndAuditTests` (миграциите върху чиста база, без чакащи промени в модела), `CatalogConstraintTests`, `DatabaseSchemaDocumentTests`, `SeedCommandTests`. Още не е в CI: `dotnet ef migrations has-pending-model-changes` (D29).

## 1. Решения

### AD1. Промяна от списъка не започва, преди да има решение

**Правило:** В `docs/decisions` има файл за решението, преди кодът да се слее, когато промяната е едно от: **нов пакет**; **нов проект или слой**; **промяна на договора на API**, която чупи клиент (преименуване или махане на поле, адрес или код на грешка); **нова външна услуга** (хранилище, поща, плащане); **нов подход за данни**, който другите обекти ще копират (версия на реда, страниране, ключове в базата); **промяна на правило** от `CLAUDE.md`, от наръчника или от решение. Проверка: за всеки от тези случаи в diff-а има нов файл `NNNN-….md` в същия pull request.

```text
// Лошо: AWSSDK.S3 е добавен в Infrastructure.csproj, а в описанието на задачата пише „снимките са в S3“.
// Никой не може да намери защо е избрано S3, кой е лицензът и какво става, ако пакетът се смени.

// Добре: 0018 записва избора преди кода, а в „Последствията“ казва какво се променя
docs/decisions/0018-product-images-in-s3-compatible-storage.md
```

**Образец:** [0018](../decisions/0018-product-images-in-s3-compatible-storage.md) (външна услуга и пакет), [0015](../decisions/0015-data-protection-keys-in-database.md) (пакет и таблица), [0016](../decisions/0016-admin-categories-list-and-row-version.md) (нов подход, който другите обекти копират). **Днес:** Да.

### AD2. Решение не се пише за това, което е следване на вече решен подход

**Правило:** Не се пише ново решение за: нова колона или ограничение по вече записания подход; нов адрес по образеца на съществуващ; рефакториране без промяна на договора; версия на реда на нов обект по [0016](../decisions/0016-admin-categories-list-and-row-version.md) без ново правило. Това е ред в описанието на задачата и в [architecture.md](../architecture.md), ако се е променила картата. Решение, което само повтаря друго, се чете като шум и скрива истинските.

```text
// Лошо: решение „Продуктите имат версия на реда“ — същото като 0016, без нищо ново
docs/decisions/0021-product-row-version.md

// Добре: версията на продукта следва 0016 и е само ред в описанието на задача 12
„Версия на реда: образецът от категориите (решение 0016)“
```

Решение за версията на настройките (0019) е оправдано, защото носи ново правило: заключване на валутата.

**Образец:** [задача 12](../tasks/12-admin-products-and-variants.md) (версията на продукта без ново решение) и [0019](../decisions/0019-store-settings-row-version-and-currency-lock.md) (с ново правило). **Днес:** Да.

### AD3. Решението е по шаблона, влиза в списъка и не се редактира след приемане

**Правило:** Файлът е `NNNN-кратко-име.md` със следващия свободен номер, по шаблона от [decisions/README.md](../decisions/README.md): дата, статус, контекст, решение, последствия. Редът в списъка на решенията влиза в **същия** pull request. Прието решение не се пренаписва: ново решение го заменя и старото получава „Статус: заменено от NNNN“. „Последствия“ казват поне дали се променят схемата, `openapi.json` и документите.

```text
// Лошо: решението е запазено, но не е в списъка; следващият човек не го вижда
docs/decisions/0005-tolerate-minimatch-dev-alert.md   (няма ред в README.md)

// Добре
- [0019 – Настройките на магазина се пазят с версия на реда, …](0019-….md)
```

**Образец:** [0019](../decisions/0019-store-settings-row-version-and-currency-lock.md) („Последствия“ казват, че няма нова колона и какво се променя в `database-schema.md`). **Днес:** **Не**: [0005](../decisions/0005-tolerate-minimatch-dev-alert.md) липсва в списъка на `decisions/README.md` (D46).

## 2. Граници между проекти

### AD4. Проектите се реферират само навътре

**Правило:** `ProjectReference` в `.csproj` е точно този:

| Проект           | Може да реферира                | Не може                 |
| ---------------- | ------------------------------- | ----------------------- |
| `Domain`         | нищо                            | всичко                  |
| `Application`    | `Domain`                        | `Infrastructure`, `Api` |
| `Infrastructure` | `Application`, `Domain`         | `Api`                   |
| `Api`            | `Application`, `Infrastructure` | `Domain` (директно)     |

`Api` реферира `Infrastructure` само за да я сглоби (`AddInfrastructure`, командите `migrate` и `seed`); контролерите говорят само с `Application`. Нов проект е решение (AD1). Проверка: `ProjectReference` в четирите `.csproj`.

```xml
<!-- Лошо: Application реферира Infrastructure, за да вземе DbContext -->
<ProjectReference Include="..\Storefront.Infrastructure\Storefront.Infrastructure.csproj" />

<!-- Добре: Application реферира само Domain -->
<ProjectReference Include="..\Storefront.Domain\Storefront.Domain.csproj" />
```

**Образец:** [`Storefront.Application.csproj`](../../src/backend/Storefront.Application/Storefront.Application.csproj) и [`Storefront.Domain.csproj`](../../src/backend/Storefront.Domain/Storefront.Domain.csproj) (празен: без проекти и пакети). **Днес:** Да. Автоматичната проверка е в 16c (D14).

### AD5. Пакетите за достъп до данни са само в `Infrastructure`

**Правило:** `Domain` и `Application` нямат нито един `PackageReference`. `EF Core`, `Npgsql`, `Identity`, `Data Protection` и `AWSSDK` са в `Infrastructure`. `Api` има само това, което му трябва за HTTP (`Microsoft.AspNetCore.OpenApi`). Нов пакет в някой проект е решение и одобрение (AD1, `CLAUDE.md`).

```xml
<!-- Лошо: Application внася EF Core „само за един Include“ -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.14" />

<!-- Добре: Application няма пакети; четенето е зад интерфейс от Contracts/ -->
```

**Образец:** [`Storefront.Infrastructure.csproj`](../../src/backend/Storefront.Infrastructure/Storefront.Infrastructure.csproj) (всички пакети за данни и хранилище). **Днес:** Да.

### AD6. `Api` не ползва `DbContext` и repository

**Правило:** В `Storefront.Api` няма `StorefrontDbContext`, `IRepositoryManager` и repository (правило 7 на [0017](../decisions/0017-api-structure-and-style.md) и таблицата в [architecture.md](../architecture.md)). Единственото място, където `Api` влиза в `Infrastructure`, е `AddInfrastructure` и командите за стартиране. Командите `migrate` и `create-admin` ловят `NpgsqlException`, за да не покажат адрес и потребител на базата; това е допустимо само в `Commands/`, защото там е корен на сглобяването, не логика.

```csharp
// Лошо: контролер чете директно от базата
public sealed class ProductsController(StorefrontDbContext context) : ControllerBase

// Добре: контролер вика услугата
public sealed class AdminProductsController(IServiceManager services) : ControllerBase
```

**Образец:** [`AdminCategoriesController`](../../src/backend/Storefront.Api/Controllers/AdminCategoriesController.cs) и другите контролери на каталога (приемат `IServiceManager`). **Днес:** Да (`grep` за `StorefrontDbContext` и `IRepositoryManager` в `Storefront.Api` няма резултат; `AuthController` и `HealthController` ползват Identity и проба на базата, не `IServiceManager`, както казва 0017).

### AD7. Интерфейсът стои при този, който го ползва, а реализацията при технологията

**Правило:** Договор към нещо външно (база, хранилище, проба на схемата) е интерфейс в `Application` (`Contracts/`, `Migrations/`, `Seeding/`), а реализацията му е в `Infrastructure`. `Application` така не знае за EF и S3 и се тества с ръчна имитация (T5 в [testing.md](testing.md)).

```csharp
// Лошо: Application вика Npgsql, за да провери схемата
await using var connection = new NpgsqlConnection(connectionString);

// Добре: Application вика интерфейс
public interface ISchemaProbe
{
    Task<bool> IsUpToDateAsync(CancellationToken cancellationToken);
}
```

**Образец:** `ISchemaProbe` в `Application/Seeding/` и `MigrationSchemaProbe` в `Infrastructure/Persistence/`; `IDatabaseMigrator` и `EfDatabaseMigrator`. **Днес:** Да.

## 3. Имена в базата

Общото (`snake_case`, множествено число, `IX_` по подразбиране, `ux_` за частичен) е в B20. Тук е допълнението.

### AD8. Имената на колоните идват от свойството, не от ръчно име

**Правило:** Колоната се получава от името на свойството по конвенцията в `StorefrontDbContext` (`LeadTimeDays` става `lead_time_days`). `HasColumnName` е допустим само за системна колона на PostgreSQL (`xmin`). Нова колона = ново свойство, без конфигурация за името.

```csharp
// Лошо: две имена за едно нещо
builder.Property(p => p.LeadTimeDays).HasColumnName("LeadTime");

// Добре: свойство LeadTimeDays → колона lead_time_days; в CHECK се пише същото име
"lead_time_days IS NULL OR lead_time_days > 0"
```

**Образец:** [`StorefrontDbContext`](../../src/backend/Storefront.Infrastructure/Persistence/StorefrontDbContext.cs) (`ToSnakeCase`) и `ProductConfiguration` (единственото `HasColumnName` е за `xmin`). **Днес:** Да.

### AD9. Ограничението има име `ck_<таблица>_<какво>` и казва правилото

**Правило:** `CHECK` се създава с `HasCheckConstraint(име, израз)` в конфигурацията на обекта, а името е `ck_<таблица>_<какво>` (`ck_products_lead_time_positive`). Едно ограничение е едно правило; изразът ползва имената на колоните, не на свойствата. Enum се пази като текст (`HasConversion<string>()`) заедно с `CHECK … IN (…)`, за да отхвърля база и ръчен запис.

```csharp
// Лошо: безименно ограничение, две правила в едно
table.HasCheckConstraint("CK_1", "price >= 0 AND stock_quantity >= 0");

// Добре
table.HasCheckConstraint("ck_product_variants_price", "price >= 0");
table.HasCheckConstraint("ck_products_status", "status IN ('Draft', 'Published', 'Archived')");
```

**Образец:** [`ProductConfiguration`](../../src/backend/Storefront.Infrastructure/Persistence/Configurations/ProductConfiguration.cs), [`StoreSettingsConfiguration`](../../src/backend/Storefront.Infrastructure/Persistence/Configurations/StoreSettingsConfiguration.cs) (`ck_store_settings_single_row`). **Днес:** Да.

### AD10. Име на индекс, което се превежда към грешка, не се сменя без речника

**Правило:** `SaveErrorTranslator.UniqueIndexes` е речник от име на индекс към код на грешка. Смяна на името на такъв индекс (или нов уникален индекс с правило за потребителя) се прави **заедно** със запис в речника и тест в `CatalogConstraintTests`. Иначе нарушението става `500`, а не `409` с код.

```csharp
// Лошо: индексът е преименуван, речникът още е със старото име
.HasDatabaseName("ux_products_slug")          // речникът: ["IX_products_slug"] → вече не съвпада

// Добре: името в речника съвпада с името в базата
["ux_product_images_one_primary_per_product"] = ("…", "image.primary_conflict"),
```

**Образец:** [`SaveErrorTranslator`](../../src/backend/Storefront.Infrastructure/Repositories/SaveErrorTranslator.cs) и `CatalogConstraintTests.Image_AllowsOnlyOnePrimaryPerProduct`. **Днес:** Да (за всеки запис в речника има тест на ограничението).

### AD11. Видът на колоната по вид данни е един и същ във всички таблици

**Правило:** Нов обект следва вече използваните видове: ключ `Guid` без генериране от базата (`ValueGeneratedNever`, стойност `Guid.CreateVersion7()` от `Entity`); време `DateTimeOffset` (`timestamp with time zone`); пари цяло число в най-малката единица (B21); enum като текст с `CHECK` (AD9); свободна структура `jsonb`. Друг вид (например `decimal` за пари, `timestamp` без зона) е решение.

```csharp
// Лошо: цена като decimal и време без зона
public decimal Price { get; set; }
public DateTime CreatedAt { get; set; }

// Добре: както в Domain
public long Price { get; set; }          // най-малката единица
public DateTimeOffset CreatedAt { get; set; }
```

**Образец:** [`Entity`](../../src/backend/Storefront.Domain/Entity.cs), `ProductVariant.Price`, `CatalogConstraintTests.Attributes_AreStoredAsJsonb_AndRoundTrip`. **Днес:** Да.

## 4. Миграции на безопасни стъпки

Колко миграции, как се казват и че не се редактират след merge са B22–B24. Тук е какво е безопасно вътре в тях.

### AD12. Генерираната миграция се чете ред по ред, преди да влезе

**Правило:** След `dotnet ef migrations add` миграцията се отваря и се проверява: има ли `DropColumn`, `DropTable` или `AlterColumn`, които не сте очаквали; има ли `AddColumn` за `xmin`; прави ли това, което казва името ѝ. Съмнителна част се поправя на ръка, с коментар `<remarks>` защо. EF не вижда разлика между „преименувано“ и „изтрито и добавено“.

```csharp
// Лошо: EF иска да добави xmin; PostgreSQL отхвърля запазеното име и миграцията пропада
migrationBuilder.AddColumn<uint>(name: "xmin", table: "categories", type: "xid", rowVersion: true, nullable: false);

// Добре: Up и Down са празни нарочно и казват защо
/// <remarks>Category.Version is mapped to xmin, a system column that PostgreSQL adds itself … only the model snapshot changes.</remarks>
```

**Образец:** [`CategoryRowVersion`](../../src/backend/Storefront.Infrastructure/Persistence/Migrations/20261109152317_CategoryRowVersion.cs) и [`ProductRowVersionAndLeadTime`](../../src/backend/Storefront.Infrastructure/Persistence/Migrations/20261110093657_ProductRowVersionAndLeadTime.cs) (ръчно изрязана колона). **Днес:** Да.

### AD13. Колона в таблица с данни се добавя, така че вече съществуващите редове да са валидни

**Правило:** `AddColumn` с `nullable: false` към таблица, която вече има редове, е с `defaultValue` или е на две стъпки: добавя се `nullable: true`, редовете се попълват (`UpdateData` или `Sql`), и чак в следваща миграция колоната става `NOT NULL`. Проверка: `AddColumn` без `nullable: true` и без `defaultValue` върху непразна таблица е грешка при преглед.

```csharp
// Лошо: пропада на база с редове („column contains null values“)
migrationBuilder.AddColumn<string>(name: "color", table: "product_variants", nullable: false);

// Добре, стъпка 1 (миграция A)
migrationBuilder.AddColumn<string>(name: "color", table: "product_variants", type: "character varying(50)", maxLength: 50, nullable: true);
// … стъпка 2 (миграция B, след като всички редове имат стойност): AlterColumn с nullable: false
```

**Образец:** няма случай в репото (единствената такава добавка, `lead_time_days`, е `NULL`-ируема и се налага с `ck_products_lead_time_for_made_to_order`). Описание на стъпките: [Customize migration code](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing). **Днес:** Няма случай.

### AD14. Преименуване, смяна на тип и разделяне на колона са в четири стъпки

**Правило:** Промяна, която може да загуби или обърка данни, не се прави с едно `RenameColumn` или `AlterColumn` в един release. Стъпките са: **(1) разширяване**: добавя се новата колона заедно със старата; **(2) копиране**: данните се пренасят (миграция със `Sql` или команда), кодът пише и в двете; **(3) смяна**: кодът чете само от новата; **(4) махане**: старата колона се маха в **отделна** миграция, когато никой работещ release не я чете. Всяка стъпка е свой pull request (B26) и може да се върне.

```csharp
// Лошо: една миграция, ако стане грешка в средата, данните от стария ред са изгубени
migrationBuilder.DropColumn(name: "short_description", table: "products");
migrationBuilder.AddColumn<string>(name: "summary", table: "products", nullable: false, defaultValue: "");

// Добре: стъпка 1 добавя summary (NULL), стъпка 2 копира
migrationBuilder.Sql("UPDATE products SET summary = short_description WHERE summary IS NULL;");
// стъпка 3: кодът чете summary; стъпка 4: DropColumn на short_description, в друга миграция
```

(Примерът е илюстрация, колоната `summary` не съществува.)

**Образец:** няма случай в репото. Разбор: [Customize migration code](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing). **Днес:** Няма случай (B26).

### AD15. Новата схема работи със стария код, защото миграцията се пуска на ръка

**Правило:** В демото `migrate` се пуска на ръка след внедряване (решение 0012), а при няколко копия за кратко работят стар и нов код. Затова всяка миграция е съвместима със **стария** release: добавя, не маха и не преименува. Премахващата стъпка (AD14, стъпка 4) влиза най-рано в release, след като новият код вече е навсякъде. Проверка: в миграцията няма `DropColumn`, `DropTable` или `RenameColumn` за колона, която кодът на предишния release още чете.

```text
// Лошо: pull request маха колоната и кода, който я чете; стара инстанция още чете колоната → 500
// Добре: release N добавя и копира; release N+1 маха (след като N е навсякъде и migrate е пуснат)
```

**Образец:** [0012](../decisions/0012-demo-migrations-and-seed-run-by-hand.md) („Последствия“: ново внедряване с нова миграция изисква ръчно `migrate`); [`ProductRowVersionAndLeadTime`](../../src/backend/Storefront.Infrastructure/Persistence/Migrations/20261110093657_ProductRowVersionAndLeadTime.cs) (само добавя ограничение). **Днес:** Да.

### AD16. Миграция, която пренася данни, се изпробва върху база с данни от предишната версия

**Правило:** Освен върху чиста база (B24), миграция със `Sql`, `UpdateData` или смяна на тип се изпробва върху база, в която вече са демо данните на предишната схема. Ред: `migrate` до предишната миграция → `seed` → `migrate` до новата → проверка, че броят и стойностите на редовете са същите. Резултатът (брой редове преди и след) е в описанието на pull request-а или в интеграционен тест.

```text
// Лошо: „миграцията минава върху чиста база“ — празни таблици не показват нищо
// Добре: migrate до предишната → seed → migrate до новата → SELECT count(*) преди и след съвпада
```

**Образец:** [`MigrateCommandTests`](../../tests/Storefront.IntegrationTests/Persistence/MigrateCommandTests.cs) (чиста база и повторно пускане) и [`SeedCommandTests`](../../tests/Storefront.IntegrationTests/Persistence/SeedCommandTests.cs) (демо данните). **Днес:** Няма случай.

## 5. Индекси и заявки

### AD17. Всеки външен ключ има индекс, чието водещо поле е ключът

**Правило:** PostgreSQL не прави индекс сам за външен ключ. За всеки `HasForeignKey` има индекс, в който колоната на ключа е **първа** (`IX_products_category_id_status` покрива `category_id`). Частичен индекс (`WHERE is_primary`) **не** покрива ключа за всички редове. Проверка: `database-schema.md` за всеки външен ключ показва индекс с водеща колона ключа.

```csharp
// Лошо: product_id има само частичен уникален индекс за главната снимка;
// търсенето на всички снимки на продукт и проверката при изтриване на продукт четат цялата таблица
builder.HasIndex(i => i.ProductId).IsUnique().HasFilter("is_primary");

// Добре: частичният индекс пази правилото, а обикновеният — достъпа
builder.HasIndex(i => i.ProductId);
builder.HasIndex(i => i.ProductId).IsUnique().HasFilter("is_primary").HasDatabaseName("ux_product_images_one_primary_per_product");
```

**Образец:** `IX_product_variants_product_id`, `IX_categories_parent_id`, `IX_products_category_id_status` в [database-schema.md](../database-schema.md). **Днес:** **Не**: `product_images.product_id` няма обикновен индекс (D45).

### AD18. Индексът е за заявка, която може да се назове

**Правило:** Нов индекс има причина от списъка: външен ключ (AD17), уникално правило, филтър или подредба на конкретен списък. Индекс „за всеки случай“ забавя запис и заема място. Причината е в името на теста или в кратък коментар в конфигурацията; индекс без причина се маха при преглед.

```csharp
// Лошо: няма заявка, която го ползва
builder.HasIndex(p => p.SeoTitle);

// Добре: филтърът на каталога е по категория и статус
builder.HasIndex(p => new { p.CategoryId, p.Status });
```

**Образец:** `ProductConfiguration` и тестът `Products_HaveIndexOnCategoryAndStatus`. **Днес:** Да (всеки индекс е за ключ, уникалност или филтър).

### AD19. Планът на нова заявка за списък се гледа върху количество, близко до реалното

**Правило:** Нова заявка, която филтрира или подрежда списък (не поглед на един ред по ключ), се проверява с `EXPLAIN (ANALYZE, BUFFERS)` върху база с хиляди реда, не върху 16-те демо продукта. С малка таблица PostgreSQL избира последователно четене и без индекс, затова планът не доказва нищо. В описанието на pull request-а стои редът от плана за таблицата (`Index Scan` или `Seq Scan`) и защо е приемлив.

```sql
-- Лошо: „върви бързо“, измерено върху демото
-- Добре
EXPLAIN (ANALYZE, BUFFERS)
SELECT id, name FROM products WHERE category_id = '…' AND status = 'Published' ORDER BY name, id LIMIT 24;
-- очаквано: Index Scan using "IX_products_category_id_status", а не Seq Scan при 10 000 реда
```

**Образец:** няма случай в репото (в кода и документите няма проверка на план). Описание: [Using EXPLAIN](https://www.postgresql.org/docs/17/using-explain.html). **Днес:** Няма случай.

### AD20. Страница се подрежда по ключ, който прави реда еднозначен

**Правило:** Всяка подредба на списък със страниране завършва с поле с уникални стойности (`Id`, или `Slug`, ако е уникален). Иначе два реда с равни стойности могат да си сменят местата между две заявки и един ред се показва два пъти или изчезва. Размерът на страницата и максимумът са в B10 и B16.

```csharp
// Лошо: равни имена; редът между страниците е случаен
.OrderBy(p => p.Name).Skip(skip).Take(take)

// Добре: коментарът в кода обяснява защо
.OrderBy(p => p.Name).ThenBy(p => p.Id).Skip(skip).Take(take)
```

**Образец:** [`CatalogQueries.GetProductsAsync`](../../src/backend/Storefront.Infrastructure/Repositories/CatalogQueries.cs) („Every order ends with the id…“); списъкът за картата на сайта е по `Slug`, който е уникален. **Днес:** Да.

## 6. Съгласуваност: версия на реда

### AD21. Обект с форма за редакция в админа, който повече от един човек може да променя, има версия на реда

**Правило:** Ако мълчаливото презаписване би загубило чужда работа (две редакции на един и същи ред), обектът има `uint Version`, свързано с `xmin`. Обект без версия е решение, записано някъде (AD1, AD2): който е по-малък и се променя през родителя, може да е без. Днес с версия: `Category`, `Product`, `StoreSettings`.

```csharp
// Лошо: форма за редакция на нов обект без версия; вторият запис презаписва първия
public sealed record UpdatePromotion(string Name, int PercentOff);

// Добре: входният модел носи версията (B9)
public sealed record UpdateCategory(string Name, string Slug, /* … */ uint Version);
```

**Образец:** [0016](../decisions/0016-admin-categories-list-and-row-version.md), [0019](../decisions/0019-store-settings-row-version-and-currency-lock.md). **Днес:** Да за трите обекта. Вариантите и снимките са без версия по препоръка от описанието на [задача 12](../tasks/12-admin-products-and-variants.md); отделно решение няма (D48).

### AD22. Версия на нов обект се добавя на шест места и миграцията е празна

**Правило:** Нова версия на реда е: **(1)** `public uint Version { get; set; }` в `Domain`; **(2)** конфигурация `HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()`; **(3)** миграция с празни `Up` и `Down` и `<remarks>` (AD12); **(4)** `ExpectVersion` в repository и **първа стъпка** на всяка промяна в услугата, преди да се пипат полетата (B6); **(5)** запис в `SaveErrorTranslator.ConcurrencyCode` с код `<обект>.version_conflict`; **(6)** `Version` във входния модел и в DTO, и тест за остаряла версия. Липсва ли едно от шестте, конфликтът не се хваща или става `500`.

```csharp
// Лошо: версията е в модела, но речникът не я знае → DbUpdateConcurrencyException стига до потребителя като 500
Category => "category.version_conflict",     // няма ред за новия обект

// Добре: и трите са в ConcurrencyCode
Category => "category.version_conflict",
Product => "product.version_conflict",
StoreSettings => "store_settings.version_conflict",
```

**Образец:** [`StoreSettingsConfiguration`](../../src/backend/Storefront.Infrastructure/Persistence/Configurations/StoreSettingsConfiguration.cs), [`StoreSettingsRepository.ExpectVersion`](../../src/backend/Storefront.Infrastructure/Repositories/StoreSettingsRepository.cs), тест `Product_SavedWithAStaleVersion_IsRejected`. **Днес:** Да.

### AD23. Конфликтът е `409` с код, а клиентът различава кодовете

**Правило:** Остаряла версия връща `409` с код `<обект>.version_conflict`. Клиентът не презаписва мълчаливо, а показва „Записът е променен от друг“ и предлага презареждане. Различава се по полето `code`, защото `slug_taken` също е `409`. Версията не е време: не се показва като „последна промяна“, не се ползва за кеш и не се сравнява по големина (`xmin` се върти и не расте стриктно). За тези нужди има `UpdatedAt`.

```ts
// Лошо: всеки 409 е „слъгът е зает“
if (error.status === 409) setFieldError("slug", t("slugTaken"));

// Добре: по кода
if (error.code === "category.version_conflict") showReloadNotice();
else if (error.code === "category.slug_taken")
  setFieldError("slug", t("slugTaken"));
```

**Образец:** [0016](../decisions/0016-admin-categories-list-and-row-version.md) („Последствия“: панелът различава кодовете; там е и защо е избрана `xmin`, а не `updated_at`). **Днес:** Да.

## 7. Демо данни и seed

### AD24. Seed е идемпотентен: повторното пускане не променя и не дублира

**Правило:** Seed разпознава записите по естествен ключ (`slug` на категория и продукт, `sku` на вариант), не по `Id`. Повторно пускане върху пълна база не създава, не променя и не трие. Ако един продукт е създаден само наполовина (прекъсване), следващото пускане го довършва или го докладва, а не го пропуска мълчаливо.

```csharp
// Лошо: продуктът съществува (само с заглавието) → пропуска се; без варианти и снимки остава завинаги
if (await repositories.Product.ExistsBySlugAsync(product.Slug, exceptProductId: null, ct)) { skipped++; continue; }

// Добре: проверка по ключ и за недостигащите части, или цялото създаване в една транзакция
```

**Образец:** [`DemoDataSeeder`](../../src/backend/Storefront.Application/Seeding/DemoDataSeeder.cs) и тест `Seed_RunTwice_ChangesNothingAndStillSucceeds`. **Днес:** **Не** изцяло: пълна база е безопасна, но наполовина създаден продукт се пропуска (D47).

### AD25. Seed създава данните през услугите, не директно

**Правило:** Демо данните се зареждат през `IServiceManager`, така че важат същите правила като за всеки запис (слъг, SKU, цени, един главен образ). Seed не вика `DbContext`, не пише SQL и не заобикаля валидацията. Така демото не може да съдържа данни, които администраторът не би могъл да създаде.

```csharp
// Лошо: директен запис, който пропуска правилата
context.Products.Add(new Product { Slug = "x", Status = ProductStatus.Published });

// Добре: същият път като в админа
var productId = await services.Product.CreateAsync(new NewProduct(/* … */), cancellationToken);
```

**Образец:** [`DemoDataSeeder.CreateProductAsync`](../../src/backend/Storefront.Application/Seeding/DemoDataSeeder.cs). **Днес:** Да.

### AD26. Seed работи само в Development и Demo и не пипа схемата

**Правило:** Позволените среди са константа в кода (`SeedEnvironments`), не настройка, за да не може да се включи в продукция с променлива. Seed отказва при липсваща или неактуална схема и **не** прилага миграции: те са отделна стъпка (B27, [0012](../decisions/0012-demo-migrations-and-seed-run-by-hand.md)). Редът в демото е винаги `migrate`, после `seed`.

```csharp
// Лошо: позволените среди идват от конфигурация; seed сам „оправя“ схемата
if (config["Seed:Enabled"] == "true") { await context.Database.MigrateAsync(); }

// Добре
SeedEnvironments.EnsureAllowed(environmentName);
if (!await schemaProbe.IsUpToDateAsync(cancellationToken)) { throw new SeedRefusedException(/* … */); }
```

**Образец:** [`SeedEnvironments`](../../src/backend/Storefront.Application/Seeding/SeedEnvironments.cs), тестове `Seed_OutsideDevelopmentAndDemo_IsRefusedAndWritesNothing` и `Seed_WithoutTheSchema_IsRefusedAndDoesNotApplyMigrations`. **Днес:** Да.

### AD27. Демо данните са еднакви при всяко пускане и без лични данни

**Правило:** Планът на демото е чист и повторяем: случайност само със **зададено** начално число, подредена винаги по един и същ начин, без `DateTime.Now`, `Guid.NewGuid()` или текущо време в стойностите. Два пъти върху чиста база дават същите имена, слъгове, SKU, цени и наличности. В демото няма истински имена, имейли, телефони ([security.md](security.md), „Лични данни“).

```csharp
// Лошо: всяко пускане дава други цени
var price = Random.Shared.Next(500, 9000);

// Добре: фиксирано начално число, винаги в един и същ ред
var random = new Random(RandomSeed);   // RandomSeed = 42
```

**Образец:** [`DemoCatalog`](../../src/backend/Storefront.Application/Seeding/DemoCatalog.cs), тестове `Seed_OnTwoCleanDatabases_GivesTheSameNamesSlugsSkusPricesAndStock` и `Plan_IsTheSameEveryTime`. **Днес:** Да.

## 8. Генерираният `database-schema.md`

### AD28. Схемата в документа се генерира и промяната ѝ е в същия pull request като миграцията

**Правило:** `docs/database-schema.md` не се редактира на ръка (решение [0007](../decisions/0007-generated-database-schema-document.md)). Всяка миграция, дори празна, се придружава от обновен файл, защото името на последната миграция е в него. Обновява се с `UPDATE_SCHEMA_DOC=1 dotnet test tests/Storefront.IntegrationTests --filter DatabaseSchemaDocumentTests` (изисква Docker). `DatabaseSchemaDocumentTests` пропада в CI, ако файлът и миграциите се разминават.

```text
// Лошо: миграция в pull request-а, а database-schema.md не е променен → CI пада
// Лошо: ред в database-schema.md, добавен на ръка → следващото генериране го трие
// Добре: миграция + генериран diff на документа; прегледът чете diff-а на документа като ефект на миграцията
```

**Образец:** [`DatabaseSchemaDocumentTests`](../../tests/Storefront.IntegrationTests/Persistence/DatabaseSchemaDocumentTests.cs) и `SchemaDocumentRenderer`; [0019](../decisions/0019-store-settings-row-version-and-currency-lock.md) („schema.md се променя само с името на последната миграция“). **Днес:** Да.

### AD29. Бизнес правилата са в `data-model.md`, а `database-schema.md` показва само как са реализирани

**Правило:** Защо има ограничение, какво означава статус и кои стойности са позволени, е в [data-model.md](../data-model.md). `database-schema.md` не обяснява защо и не добавя правила. Ново ограничение в базата, което е бизнес правило, се описва първо в `data-model.md` (чеклистът на `CLAUDE.md` изисква обновен документ при промяна). Не се копират текстове между двата файла.

```text
// Лошо: обяснение „защо“ добавено в генерирания файл → изчезва при следващото генериране
// Добре: „защо“ в data-model.md; в базата — ck_products_lead_time_for_made_to_order със същото условие
```

**Образец:** [data-model.md](../data-model.md) и [database-schema.md](../database-schema.md). **Днес:** Да.

## Как правилата се свързват с другите документи

| Тук                          | Допълва / се основава на                                                                                                                                                              | Разлика                                                                     |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------- |
| AD1–AD3 (решения)            | [decisions/README.md](../decisions/README.md), `CLAUDE.md` („Първо пита“)                                                                                                             | CLAUDE.md казва, че се пита; тук е кога се пише решение и как               |
| AD4–AD7 (граници)            | [architecture.md](../architecture.md), [0006](../decisions/0006-book-style-layers-and-controllers.md), [0017](../decisions/0017-api-structure-and-style.md)                           | architecture.md е картата; тук е проверимото правило (`.csproj`, пакети)    |
| AD8–AD11 (имена)             | B18–B21 в [backend.md](backend.md)                                                                                                                                                    | B20 е общото име; тук колони, ограничения, имена и речникът на грешките     |
| AD12–AD16 (миграции)         | B22–B27 в [backend.md](backend.md)                                                                                                                                                    | B26 сочи към тук за стъпките; B22–B24 са име, без редакция и обратимост     |
| AD17–AD20 (индекси и заявки) | B15–B17 в [backend.md](backend.md), CC20–CC21 в [csharp-golden-rules.md](csharp-golden-rules.md)                                                                                      | B15–B17 са за заявките, тук за индексите и плана                            |
| AD21–AD23 (версия на реда)   | B6, B12–B13 в [backend.md](backend.md), [0016](../decisions/0016-admin-categories-list-and-row-version.md), [0019](../decisions/0019-store-settings-row-version-and-currency-lock.md) | B6 е `ExpectVersion`; тук кога и на колко места се добавя                   |
| AD24–AD27 (seed)             | [0012](../decisions/0012-demo-migrations-and-seed-run-by-hand.md), B27                                                                                                                | 0012 е решението за ръчното пускане; тук са правилата за самия seed         |
| AD28–AD29 (схема)            | B28 в [backend.md](backend.md), [0007](../decisions/0007-generated-database-schema-document.md)                                                                                       | B28 е „обнови“; тук е какво не се прави и кой документ какво носи           |
| Изолация на базата в тестове | [test-writing-rules.md](test-writing-rules.md) (TW16–TW17), `PostgresFixture`                                                                                                         | Всеки тест има своя база (`CREATE DATABASE test_<guid>`); не се повтаря тук |

## Ред в дълга от този файл

- AD3: `0005` липсва в списъка на решенията (D46).
- AD17: `product_images.product_id` няма обикновен индекс (D45).
- AD21: липсата на версия на вариант и снимка е записана само в описанието на задача 12 (D48).
- AD24: наполовина създаден продукт се пропуска при повторно пускане на seed (D47).
