# Златни правила: C#

Как се пише един клас, един метод и една грешка в `Application` и `Infrastructure`. [backend.md](backend.md) казва **къде** стои кодът и колко голям е; този файл казва **как изглежда**. Правилата не повтарят другите документи, а ги допълват и сочат към тях (таблицата в края показва връзките).

**Формат.** Всяко правило е в една рамка: **правило** (отговор „да“ или „не“) → **лош пример** → **добър пример** → **образец** от репото или от документацията. Редът „Днес“ казва дали кодът го следва; „не“ е ред в [tech-debt.md](../tech-debt.md). Номерата са `CC1`, `CC2`… (C# code), за да не се бъркат с `B` (подредба), `T` (тестове), `R` (рефакториране) и `RC` (React).

**Версии.** .NET 10, C# 14, `Nullable` включен, `TreatWarningsAsErrors` и `EnforceCodeStyleInBuild` (`Directory.Build.props`), EF Core 10, Npgsql.

**Източници.** „Ultimate ASP.NET Core Web API“ на Code Maze (услуги, изключения и логове), през [0008](../decisions/0008-code-style-reference-book.md). Microsoft Learn: [Nullable reference types](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references), [Async best practices](https://learn.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming), [ASP.NET Core Best Practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices), [Handle errors](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling), [Logging](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/), [Exception best practices](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions), [Coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).

**Какво не е тук.** Размер на услуга, `FieldErrors`, `ExpectVersion`, DTO, входни модели, кодове на грешки (формат), `IQueryable`, `Include`, страниране, миграции и имена в базата са в [backend.md](backend.md) (B1–B28). Тук не се повтарят.

**Как се налага.** Автоматично: `Nullable`, предупрежденията като грешки, `CS8618` като грешка и `.editorconfig` (файлови пространства от имена, винаги къдрави скоби, `var`) в build; `dotnet format` в CI (CC1, CC15, CC31). Останалото е при преглед; 16c може да добави още.

## 1. Услуга

### CC1. Зависимостите идват през конструктора, а състоянието не се пази между извикванията

**Правило:** Услугата получава всичко през конструктора (основен конструктор). Няма `static` поле, което се променя, няма търсене на услуга от `IServiceProvider`, няма `new` на друга услуга или на repository. В полетата на услугата стоят само вложените зависимости: данните на една заявка са в променливи на метода.

```csharp
// Лошо: общо състояние между заявките и скрита зависимост
internal sealed class CategoryService : ICategoryService
{
    private static readonly List<Category> Cache = [];
    private readonly IRepositoryManager _repositories = new RepositoryManager(new StorefrontDbContext());
}

// Добре
internal sealed class CategoryService(IRepositoryManager repositories) : ICategoryService
```

`static` е допустимо само за `const`, за `static readonly` стойност, която никога не се променя (`SaveErrorTranslator.UniqueIndexes`), и за чисти методи в `*Rules`.

**Образец:** `CategoryService`. **Днес:** Да.

### CC2. Услугата е `internal sealed`, договорът е `public`

**Правило:** Класът на услугата е `internal sealed`. Публичен е само интерфейсът ѝ (`ICategoryService`) и входните модели с DTO. `Api` вижда услугите само през `IServiceManager` ([0006](../decisions/0006-book-style-layers-and-controllers.md)).

**Образец:** `CategoryService` и `ICategoryService`. **Днес:** Да.

### CC3. Услугата се дели и когато има твърде много зависимости, не само по размер

**Правило:** Освен границата на B2 (300 реда или 12 публични метода) услуга с повече от **4** зависимости в конструктора или с две групи методи, които не делят нито един общ помощен метод, се дели по подобласт. Така `ProductService` е разделен по снимки и варианти (D22, остатъкът е D67), а не по произволна линия.

**Образец:** `ProductService(IRepositoryManager, IImageStorage)` е на границата: снимките са единствената причина за втората зависимост. **Днес:** Да за зависимостите (най-много 2); за размера виж D22 (платен), за броя методи D67.

## 2. Repository и `IRepositoryManager`

### CC4. Repository има само данни: четене, добавяне, проверка. Правилата са в услугата

**Правило:** Методите на repository-то четат обект, връщат списък или `bool`, добавят обект в `DbContext` (`Create`) или слагат очакваната версия (`ExpectVersion`). Те не валидират, не хвърлят домейнови грешки и не викат `SaveChanges`. Записът става само през `IRepositoryManager.SaveAsync`, веднъж в края (B7).

```csharp
// Лошо: правило в repository
public async Task<Category> CreateAsync(Category category, CancellationToken cancellationToken)
{
    if (await Query(false).AnyAsync(c => c.Slug == category.Slug, cancellationToken))
    {
        throw new ConflictException("The slug is already used.", "category.slug_taken");
    }

    Context.Add(category);
    await Context.SaveChangesAsync(cancellationToken);
    return category;
}

// Добре: repository дава факта, услугата решава (EnsureSlugIsFreeAsync), SaveAsync е в края
if (await repositories.Product.ExistsBySlugAsync(slug, exceptProductId, cancellationToken))
{
    throw new ConflictException($"The slug '{slug}' is already used.", "product.slug_taken");
}
```

**Образец:** `ProductRepository.ExistsBySlugAsync` и `ProductService.EnsureSlugIsFreeAsync`. **Днес:** Да: единственият `SaveChangesAsync` в `Repositories/` е в `RepositoryManager`.

### CC5. Репозиторийните методи се наричат по шаблон и `trackChanges` е явен параметър

**Правило:** `Get<Какво>[By<Ключ>]Async` връща обект или `null`; `Has<Какво>Async` и `Exists<…>Async` връщат `bool`; `Create` е без `Async`, защото не ходи до базата. Дали обектът ще бъде променян, е явен параметър `trackChanges` без стойност по подразбиране, за да се вижда на мястото на извикването.

```csharp
// Лошо: не личи дали обектът се следи
Task<Category?> GetAsync(Guid id, CancellationToken cancellationToken);

// Добре
Task<Category?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);
```

**Образец:** `ICategoryRepository`. **Днес:** Да.

## 3. Именуване

### CC6. Имената показват вида на типа

**Правило:**

| Какво                          | Правило                                                         | Образец                               |
| ------------------------------ | --------------------------------------------------------------- | ------------------------------------- |
| Интерфейс                      | `I` + име                                                       | `IRepositoryManager`, `IImageStorage` |
| Услуга                         | `<Област>Service`, интерфейс `I<Област>Service`                 | `CategoryService`                     |
| Помощен клас с правила         | `<Област>Rules`, статичен                                       | `ProductRules`, `SlugRules`           |
| Входен модел                   | `New…` или `Update…`, `record`                                  | `NewCategory`, `UpdateCategory`       |
| DTO                            | `…Dto`, `record`                                                | `CategoryAdminDto`                    |
| Изключение                     | `…Exception`                                                    | `ConflictException`                   |
| Параметри и локални променливи | `camelCase`; `CancellationToken` е `cancellationToken`, не `ct` | `CategoryService.CreateAsync`         |
| Частно поле                    | `_camelCase`                                                    | `FieldErrors._errors`                 |
| Константа                      | `PascalCase`, на върха на класа, с единица в името, ако я има   | `NameMaxLength`                       |
| Булев метод или свойство       | `Is…`, `Has…`, `Can…`, `Exists…`                                | `HasChildrenAsync`, `IsActive`        |

Имената са на английски (неотменимо правило 1 от [CLAUDE.md](../../CLAUDE.md)).

**Днес:** Да.

### CC7. `Async` суфиксът е на всеки метод, който връща `Task`, освен на действие в контролер

**Правило:** Метод, който връща `Task`, `Task<T>` или `ValueTask`, завършва на `Async`. Изключения: действие в контролер (името му е част от адреса и от `operationId`: `Get`, `Create`, `Logout`) и методи, чието име идва от интерфейс на рамката. Метод без `Task` няма суфикс, дори да вика `async` код отвътре.

```csharp
// Лошо
public Task<Category?> GetCategory(Guid id, CancellationToken cancellationToken);

// Добре
public Task<Category?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);
```

**Образец:** `ICategoryService`; действия: `AdminCategoriesController.Get`. **Днес:** Да.

### CC8. Кодът на грешка е на едно място в кода

**Правило:** Форматът `<обект>.<причина>` е в B12. Тук е допълнението: ако един и същ код се хвърля на повече от едно място, той е в метод-фабрика на класа (`CategoryNotFound(id)`), а не буквален низ, повторен на всяко място. Така съобщението и кодът не се разминават.

```csharp
// Лошо: един и същ код, две различни съобщения, на две места
throw new NotFoundException("The product was not found.", "product.not_found");
// … друг файл:
throw new NotFoundException($"Product {id} was not found.", "product.not_found");

// Добре
private static NotFoundException CategoryNotFound(Guid id) =>
    new($"Category {id} was not found.", "category.not_found");
```

**Образец:** `CategoryService.CategoryNotFound` (ползва се от `GetAsync` и `FindAsync`). **Днес:** **Не**: `product.not_found` и `store_settings.not_found` са написани на по две места в различни файлове (D38).

## 4. `async` и `CancellationToken`

### CC9. `CancellationToken` стига до последния асинхронен извикан метод

**Правило:** Всеки метод в `Application` и `Infrastructure`, който чака вход/изход, приема `CancellationToken cancellationToken` като **последен** параметър, без стойност по подразбиране, и го подава на всяко асинхронно извикване от тялото си. `CancellationToken.None` е допустимо на две места: входната точка (`Program.cs`) и почистване след грешка, което не бива да се прекъсва, с коментар защо.

```csharp
// Лошо: токенът се губи по пътя
public async Task<CategoryAdminDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
    await repositories.Catalog.GetAdminCategoryAsync(id, CancellationToken.None)
        ?? throw CategoryNotFound(id);

// Добре
public async Task<CategoryAdminDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
    await repositories.Catalog.GetAdminCategoryAsync(id, cancellationToken)
        ?? throw CategoryNotFound(id);
```

**Образец:** `CategoryService`; позволено `None`: `ProductService.UploadImageAsync` (изтриване на файла, който няма ред в базата, с коментар). **Налага се:** преглед; `CA2016` (подай токена) може да се включи в 16c. **Днес:** Да.

### CC10. Няма блокиране на асинхронен код и няма `async void`

**Правило:** Забранени са `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` и `Task.Run` в пътя на заявката. Метод, който чака, е `async Task`; `async void` се ползва само за обработчик на събитие, а такъв в `Application` и `Infrastructure` няма. Блокирането задържа нишка от пула и при натоварване спира целия сървър ([Async best practices](https://learn.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)).

```csharp
// Лошо
var category = repositories.Category.GetByIdAsync(id, true, cancellationToken).Result;

// Добре
var category = await repositories.Category.GetByIdAsync(id, true, cancellationToken);
```

**Образец:** целият `Application`. **Днес:** Да (търсене на `.Result`, `.Wait(`, `GetResult`, `async void`, `Task.Run` в `src/backend`: нито едно съвпадение).

### CC11. `ConfigureAwait(false)` не се пише

**Правило:** ASP.NET Core няма контекст на синхронизация, затова `ConfigureAwait(false)` не променя нищо и само добавя шум ([ASP.NET Core Best Practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices)). Няма го в `src/backend`.

**Днес:** Да.

### CC12. Метод, който само подава резултата нататък, връща `Task` без `async`/`await`

**Правило:** Когато методът е едно извикване без обработка на резултата, връща се директно `Task` (по-малко състояние на автомат). `async`/`await` се слага, когато има какво да се направи с резултата (`??`, `try`, `using`, повече от едно извикване). Опасността: в метод с `using` или `try` директното връщане би освободило ресурса или би изпуснало грешката преди края на задачата.

```csharp
// Добре: само предаване
public Task<IReadOnlyList<CategoryAdminDto>> GetAllAsync(CancellationToken cancellationToken) =>
    repositories.Catalog.GetAdminCategoriesAsync(cancellationToken);

// Добре: има обработка на резултата, затова await
public async Task<CategoryAdminDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
    await repositories.Catalog.GetAdminCategoryAsync(id, cancellationToken) ?? throw CategoryNotFound(id);
```

**Образец:** `CategoryService.GetAllAsync` и `GetAsync`. **Днес:** Да.

## 5. `null`

### CC13. Подтискането на `null` е с коментар

**Правило:** Оператор `!` (`value!`), `#pragma warning disable CS86xx` и `null!` са позволени само с коментар на същия или на предишния ред, който казва **защо** стойността не е `null`. Без коментар пред `!` се поправя типът или се добавя проверка. Предупрежденията са грешки ([Directory.Build.props](../../Directory.Build.props)), затова `!` е единственият начин да се заобиколи компилаторът, и точно затова трябва да се обяснява.

```csharp
// Лошо: никой не знае защо Email не е null
return new AdminUserDto(user.Email!);

// Добре: липсата е обработена
return new AdminUserDto(user.Email ?? throw new InvalidOperationException("The administrator has no email."));

// Допустимо: `!` с причина
// The caller has just checked Translate(ex) is not null in the `when` filter above.
throw SaveErrorTranslator.Translate(ex)!;
```

**Образец:** в репото няма пример с коментар; всички места са в D37. **Днес:** **Не**: `!` без коментар в `ApiExceptionHandler`, `AdminAuthService` (два пъти), `RepositoryManager` и `ProductVariantConfiguration` (D37).

### CC14. „Няма такъв“ е `null` в repository и изключение в услугата

**Правило:** Repository връща `T?`, когато редът може да не съществува. Услугата превръща това в `NotFoundException` с помощен метод (`FindAsync`) и не връща на `Api` `null` за липсващ обект. Изключения: резултат, който нарочно не казва защо липсва, и е записан в документацията на интерфейса (`IAdminAuthService.SignInAsync` връща `null` при грешен вход, за да не помага на познаване на пароли, виж `InvalidCredentialsException`).

```csharp
// Лошо: контролерът трябва да помни да провери null
public Task<CategoryAdminDto?> GetAsync(Guid id, CancellationToken cancellationToken);

// Добре: услугата гарантира, че има стойност или хвърля
public async Task<CategoryAdminDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
    await repositories.Catalog.GetAdminCategoryAsync(id, cancellationToken)
        ?? throw CategoryNotFound(id);
```

**Образец:** `CategoryService.FindAsync` и `GetAsync`. **Днес:** Да.

### CC15. Данните отвън се проверяват с валидация, а `ThrowIfNull` е само за код без анализ на `null`

**Правило:** Низ или поле, дошло от JSON или форма, се проверява във валидацията (`string.IsNullOrWhiteSpace`, `FieldErrors`) и дава `400` с поле, не `ArgumentNullException`. Параметри, които подава собственият ни код, не се проверяват за `null`: компилаторът ги пази. `ArgumentNullException.ThrowIfNull(x)` се пише само в публичен метод, който може да бъде извикан от код без включен `Nullable` (в този проект няма такъв). Затова методът за проверка на полетата приема `string?`.

```csharp
// Лошо: ArgumentNullException се превръща в 500
public static void ValidateFields(string name) => ArgumentNullException.ThrowIfNull(name);

// Добре: липсващото име е грешка по поле
private static void ValidateFields(string? name, string? slug, string? description, int sortOrder)
{
    if (string.IsNullOrWhiteSpace(name))
    {
        errors["name"] = ["Name is required."];
    }
}
```

**Образец:** `CategoryService.ValidateFields`. **Днес:** Да: няма `ThrowIfNull` и не е нужен.

## 6. Изключения

### CC16. Грешките на домейна са петте типа от `Exceptions/`, а нов тип иска обработчик и тест

**Правило:** Тип за нова грешка се добавя само заедно със запис в `ApiExceptionHandler` (B11) и тест за кода и статуса. Съобщението на изключението е на английски и е за програмиста; текстът за потребителя идва от кода на грешка във файла с преводи на фронтенда. `Application` не знае за HTTP: няма `StatusCode`, `IActionResult` и нищо от `Microsoft.AspNetCore` в изключенията.

```csharp
// Лошо: Application знае статуса на HTTP
public sealed class SlugTakenException() : Exception("Slug taken") { public int Status => 409; }

// Добре: обработчикът в Api решава статуса
ConflictException e => ApiProblems.Create(StatusCodes.Status409Conflict, "Conflict", e.Message, e.Code),
```

**Образец:** `ConflictException` и `ApiExceptionHandler`. **Днес:** Да за границата (в `Application` няма `AspNetCore`); тестът за всеки тип е при преглед.

### CC17. Изключение не замества обикновен отговор

**Правило:** Въпрос с отговор „да“ или „не“ е метод, който връща `bool` (`HasChildrenAsync`, `ExistsBySlugAsync`); изключение се хвърля едва когато услугата реши, че е грешка (`EnsureSlugIsFreeAsync`). Repository никога не хвърля домейнова грешка (CC4).

```csharp
// Лошо: изключението е начинът да се получи отговор
try { await repositories.Category.GetBySlugOrThrowAsync(slug, ct); free = false; }
catch (NotFoundException) { free = true; }

// Добре
if (await repositories.Product.ExistsBySlugAsync(slug, exceptProductId, cancellationToken)) { throw …; }
```

**Образец:** `ProductService.EnsureSlugIsFreeAsync`. **Днес:** Да.

### CC18. `catch` е само със знаен тип или с `throw;`

**Правило:** Забранени са `catch (Exception)` без филтър `when`, празен `catch` и `throw ex;` (губи стека). `catch` е допустим на три места: (1) почистване, което свършва с `throw;`; (2) превод на **конкретен** тип към друг (`catch (DbUpdateException) when …`); (3) граница към външна система, където се изброяват типовете във филтър и грешката се записва в лог (CC22). `OperationCanceledException` при отказ на клиента се пропуска, не се поглъща: филтърът добавя `&& !cancellationToken.IsCancellationRequested`.

```csharp
// Лошо: поглъща всичко, включително отказ на клиента и грешки в кода
catch (Exception)
{
    return false;
}

// Добре: известни типове, не при отказ, със запис в лога
catch (Exception exception) when (exception is NpgsqlException or OperationCanceledException or TimeoutException
    && !cancellationToken.IsCancellationRequested)
{
    logger.LogWarning(exception, "Database health check failed");
    return false;
}

// Добре: почистване, после същата грешка нагоре
catch
{
    await storage.DeleteAsync(key, CancellationToken.None);
    throw;
}
```

**Образец:** `NpgsqlDatabaseProbe`, `S3ImageStorage.DeleteAsync`, `ProductService.UploadImageAsync`, `RepositoryManager.SaveAsync`. **Днес:** Да: няма `catch (Exception)` без филтър.

### CC19. Грешка от базата се превежда на едно място и непозната грешка не се скрива

**Правило:** Превеждането на уникално нарушение и на конфликт на версия е в `SaveErrorTranslator`. Тя връща `null` за всичко непознато и тогава `SaveAsync` пуска оригиналното изключение; ново ограничение за превод се добавя в таблицата `UniqueIndexes` с тест в `CatalogConstraintTests` (B13). Съобщение, взето от текста на `PostgresException`, не се показва на потребителя.

```csharp
// Лошо: разчита на текста на съобщението
if (ex.Message.Contains("duplicate key")) { throw new ConflictException("Taken", "slug_taken"); }

// Добре: по име на ограничение и по SqlState
if (exception.InnerException is PostgresException { SqlState: UniqueViolation, ConstraintName: { } name }
    && UniqueIndexes.TryGetValue(name, out var conflict))
{
    return new ConflictException(conflict.Message, conflict.Code);
}
```

**Образец:** `SaveErrorTranslator`. **Днес:** Да.

## 7. Данни

### CC20. Проверка за съществуване, която не ползва обекта, е метод с `bool`

**Правило:** Ако на услугата трябва само „има ли такъв“, repository има метод, който връща `bool` и в базата стига до `AnyAsync` (без да чете цял ред). Зареждането на целия обект е допустимо, когато той ще се ползва след това (`FindAsync`).

```csharp
// Лошо: чете цял ред, за да провери само дали го има
_ = await repositories.Category.GetByIdAsync(categoryId, trackChanges: false, cancellationToken)
    ?? throw new NotFoundException(…);

// Добре
if (!await repositories.Category.ExistsAsync(categoryId, cancellationToken))
{
    throw new NotFoundException(…, "product.category_not_found");
}
```

**Образец:** `ProductRepository.ExistsBySlugAsync`, `CategoryRepository.HasChildrenAsync`. **Днес:** **Не**: `ProductService.EnsureCategoryExistsAsync` чете цялата категория (D39).

### CC21. Парите и времето имат един вид тип

**Правило:** Парите са `long` в най-малката единица на валутата (B21): без `decimal`, `double` и `float`. `double` е допустим за стойности, които не са пари (`ColorContrast.Ratio`). Времето в `Application` и `Infrastructure` се взема от `TimeProvider`, не от `DateTime.UtcNow`, за да може тестът да го замени; съхранява се в UTC.

```csharp
// Лошо
public decimal Price { get; set; }
var expires = DateTime.UtcNow.AddMinutes(5);

// Добре
public long Price { get; set; }
var expires = timeProvider.GetUtcNow().AddMinutes(5);
```

**Образец:** `ProductVariant.Price` (`long`). **Днес:** Да: няма `decimal`, нито `DateTime.UtcNow` в `src/backend` извън миграциите (ако се появи, е ред в регистъра).

## 8. Логове

### CC22. Шаблон със структурирани полета, не сглобен низ

**Правило:** Съобщението е постоянен шаблон с имена в `PascalCase` (`{StorageKey}`), а стойностите се подават като аргументи. Никога `$"…"`, `+` или `string.Format` в извикването на `Log…`. Изключението е **първият** аргумент (`LogWarning(exception, "…")`), не част от текста. Всяка грешка, която се поглъща (CC18, граница към външна система), оставя ред в лога; команда от `Api/Commands/` пише кратко съобщение в изхода си (`MigrateCommand`).

```csharp
// Лошо: губи се структурата и се прави низ при всяко извикване
logger.LogWarning($"Delete failed for {key}: {exception.Message}");

// Добре
logger.LogWarning(
    "An image file could not be deleted and is left in the bucket. Key: {StorageKey}. Failure: {Failure}",
    key,
    exception.GetType().Name);
```

**Образец:** `S3ImageStorage.DeleteAsync`. **Днес:** Да (търсене на `Log…($"` в `src/backend`: нищо).

### CC23. В лога няма лични данни, тайни и тела на отговори

**Правило:** Пишат се идентификатори (`Guid`, ключ на файл), видът на грешката (`exception.GetType().Name`) и причина от краен списък. Не се пишат: имейл, парола, токен, ключ за достъп, адрес, телефон, IP на клиента, цяло тяло на заявка или отговор ([backend.md](backend.md), „Логове“, и [security.md](security.md), „Лични данни“). Услугите в `Application` не ползват `ILogger`: логът е в `Infrastructure` и `Api`, там, където се поглъща грешка или се стига до външна система.

```csharp
// Лошо
logger.LogWarning("Sign-in failed for {Email} with {Password}", email, password);

// Добре: без имейл, причина от краен списък
logger.LogWarning(
    "Admin sign-in failed: {Reason}.",
    result.IsLockedOut ? "account locked out" : "wrong password");
```

**Образец:** `AdminAuthService.SignInAsync` (коментар „No email in the log“). **Днес:** Да.

## 9. Коментари и стил

### CC24. Коментарът е на английски и казва „защо“

**Правило:** Коментарите са на английски (неотменимо правило 1 от [CLAUDE.md](../../CLAUDE.md)) и обясняват причината, а не повтарят кода. Добър коментар казва какво не личи: защо редът е такъв, защо не се прави по очевидния начин, какво би се счупило. Текстове за потребителя не стоят в `.cs` файловете на `Application` (те са код на грешка, а текстът е във фронтенда); български низове има само в демо данните (`Seeding/`), които са съдържание, не интерфейс.

```csharp
// Лошо: казва какво, което се вижда
// Loop over the siblings and set the sort order.
for (var position = 0; position < siblings.Count; position++) { … }

// Добре: казва защо
// Rewrite the whole group as 0, 1, 2...: swapping two equal sortOrders would otherwise change nothing.
for (var position = 0; position < siblings.Count; position++) { … }
```

**Образец:** `CategoryService.MoveAsync`, `ProductService.UpdateAsync` („Worked out before anything is changed, so a refused request leaves the product as it was“). **Днес:** Да.

### CC25. Няма закоментиран код и няма `TODO` без задача

**Правило:** Закоментиран код се трие (историята е в git). `TODO` е позволен само във вида `// TODO(task-NN): …` със свързана задача или ред в [tech-debt.md](../tech-debt.md) (правило от [backend.md](backend.md), „Стил“).

```csharp
// Лошо
// var legacy = repositories.Category.GetAll();
// TODO fix later

// Добре: нищо, или
// TODO(task-17): cap the backup size once the bucket is chosen.
```

**Днес:** Да (в `src/backend` няма `TODO`).

### CC26. XML документация има там, където договорът не личи от подписа

**Правило:** `///` се пише на интерфейс с негативно или неочевидно поведение (кога връща `null`, кой код хвърля), на изключение със стабилен `Code`, на метод на repository с особеност (проследен ли е обектът) и на публичен тип, който друг проект ползва. Не се пише на всеки метод („Gets the id“). Документацията казва какво е гарантирано, не как е направено.

```csharp
// Лошо
/// <summary>Gets the category.</summary>
Task<Category?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);

// Добре: казва нещо, което подписът не казва
/// <summary>The categories with the same parent, ordered by sortOrder, name and id. Tracked, so the order can be rewritten.</summary>
Task<IReadOnlyList<Category>> GetSiblingsAsync(Guid? parentId, CancellationToken cancellationToken);
```

**Образец:** `ICategoryRepository.GetSiblingsAsync`, `ConflictException`. **Днес:** при преглед.

### CC27. Стилът се налага от `.editorconfig` и `dotnet format`

**Правило:** Не се спори за стил: файлови пространства от имена, винаги къдрави скоби, `var` при очевиден тип и реда на `using` са в [.editorconfig](../../.editorconfig). Едно ново правило за стил първо влиза там, не в този файл. Името на файла е името на типа в него (един публичен тип на файл).

**Налага се:** `EnforceCodeStyleInBuild` и `dotnet format` в CI. **Днес:** Да.

## Връзки с другите правила

Правилата тук допълват, не повтарят. При противоречие важи редът от [README.md](README.md).

| Тук                               | Допълва / се основава на                                                  | Разлика                                                                     |
| --------------------------------- | ------------------------------------------------------------------------- | --------------------------------------------------------------------------- |
| CC1–CC3 (услуга)                  | [backend.md](backend.md) B1–B8                                            | B казва граница в редове и къде стои, CC1–CC3 как е устроена и кога се дели |
| CC4–CC5 (repository)              | B14 (`IQueryable`), B15–B16                                               | B14–B16 са за заявките, CC4–CC5 за това какво е позволено на repository     |
| CC8 (код на грешка)               | B12                                                                       | B12 е форматът, CC8 е къде се пише кодът                                    |
| CC11 (`ConfigureAwait`)           | [0008](../decisions/0008-code-style-reference-book.md) (async навсякъде)  | Решението изисква `async` с токен; тук са детайлите                         |
| CC16, CC18–CC19 (изключения)      | B11, B13, [0017](../decisions/0017-api-structure-and-style.md)            | B11 и 0017 са за обработчика в `Api`; тук е какво се хвърля и къде се хваща |
| CC20 (`bool` за съществуване)     | B5                                                                        | B5 е помощен метод в услугата, CC20 какво връща repository                  |
| CC21 (пари)                       | B21                                                                       | Същото за парите; тук е и времето                                           |
| CC22–CC23 (логове)                | [backend.md](backend.md) („Логове“), [security.md](security.md)           | backend.md казва „структурирани, без лични данни“, тук е как изглежда       |
| CC24–CC25 (коментари)             | [backend.md](backend.md) (ред „Стил“)                                     | Същото; тук е примерът                                                      |
| Тестове на услуги                 | [testing.md](testing.md), [test-writing-rules.md](test-writing-rules.md)  | Не се повтарят тук                                                          |
| Решения, имена в базата, миграции | [architecture-and-data-rules.md](architecture-and-data-rules.md), B18–B28 | Не се повтарят тук                                                          |

## Ред в дълга от този файл

- CC8: `product.not_found` и `store_settings.not_found` са написани на по две места (D38).
- CC13: `!` без коментар на пет места (D37).
- CC20: `EnsureCategoryExistsAsync` чете цял ред за проверка на съществуване (D39).
