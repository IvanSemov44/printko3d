# Златни правила: писане на тестове

Как изглежда един добър тест. [testing.md](testing.md) казва **кога** се пише тестът и **къде** стои; този файл казва **как** се пише. Правилата не повтарят другите документи, а ги допълват и сочат към тях (таблицата в края показва връзките).

**Формат.** Всяко правило е в една рамка: **правило** (отговор „да“ или „не“) → **лош пример** → **добър пример** → **образец** от репото или от документацията. Редът „Днес“ казва дали тестовете го следват; „не“ е ред в [tech-debt.md](../tech-debt.md). Номерата са `TW1`, `TW2`… (test writing), за да не се бъркат с `T` (кога) и `CC` и `RC` (код).

**Инструменти.** Backend: xUnit 2.9, ръчно написани имитации (без пакет за имитации), Testcontainers и `WebApplicationFactory` за интеграционните. Frontend: Vitest, Testing Library (`@testing-library/react`), `vi.stubGlobal` за мрежата; `@testing-library/user-event` е одобрен, но още не е инсталиран (D28).

**Източници.** [Best practices for writing unit tests](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices) (Microsoft), [Guiding Principles](https://testing-library.com/docs/guiding-principles/), [About Queries](https://testing-library.com/docs/queries/about/) и [Common mistakes](https://kentcdodds.com/blog/common-mistakes-with-react-testing-library) (Testing Library и Kent C. Dodds), [Testing Trophy](https://kentcdodds.com/blog/the-testing-trophy-and-testing-classifications) (Kent C. Dodds) и [xUnit: Shared context](https://xunit.net/docs/shared-context).

**Как се налага.** Автоматично: `eslint-plugin-testing-library` не е в проекта (нов пакет иска одобрение), затова всичко е при преглед. 16c може да добави още.

**Какво не е тук.** Кога тестът е преди кода, къде стои файлът, помощниците за данни, границата от 500 реда и правилата T1–T6 са в [testing.md](testing.md). Не се изтриват, не се отслабват и не се пропускат тестове (T3) и не се умножава това, което се тества (T5).

## 1. Име и обхват

### TW1. Името на C# теста казва какво, при какво условие и какво се очаква

**Правило:** `Предмет_Условие_Резултат`, на английски, прочетено като изречение. Името стига, за да се разбере какво се е счупило, без да се отваря тялото. Класът е `<Област><Тема>Tests`.

```csharp
// Лошо
public async Task Test1()
public async Task CreateWorks()

// Добре
public async Task Create_WithoutName_IsRejectedOnTheNameField(string name)
public async Task Update_WithAStaleVersion_Returns409WithACode_AndKeepsTheOtherEdit()
```

**Образец:** `CategoryManagementTests`, `AdminCategoriesApiTests`. **Днес:** Да.

### TW2. Името на frontend теста е изречение за поведението

**Правило:** `describe` е предметът (компонент, функция, екран), `it` е изречение на английски, което казва какво вижда или може да направи потребителят. Не „works“, не номер, не име на вътрешна функция.

```ts
// Лошо
it('renders', …)
it('test 2', …)

// Добре
describe('Category list', () => {
  it('gives every button of a row a name that says which category it is for', …)
})
```

**Образец:** `categories.test.tsx`, `categoryValidation.test.ts`. **Днес:** Да.

### TW3. Един тест проверява едно поведение

**Правило:** Няколко проверки са допустими само ако са за **едно** поведение (статус, код на грешка и състояние след него). Тест, който създава, променя и трие, се дели на три. Ако името се нуждае от „и“ между две различни поведения, са два теста.

```csharp
// Лошо: три поведения в един тест
public async Task CategoryLifecycle() { /* create, update, delete */ }

// Добре: едно поведение, три проверки за него
var first = await host.SendAsync(HttpMethod.Put, url, Update(created, name: "First"), cookie);
var second = await host.SendAsync(HttpMethod.Put, url, Update(created, name: "Second"), cookie);

Assert.Equal(HttpStatusCode.OK, first.StatusCode);
Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
Assert.Equal("category.version_conflict", (await JsonOf(second)).GetProperty("code").GetString());
```

**Образец:** `AdminCategoriesApiTests.Update_WithAStaleVersion_Returns409WithACode_AndKeepsTheOtherEdit`. **Днес:** при преглед.

## 2. Подредба

### TW4. Подготовка, действие, проверка: три блока, разделени с празен ред

**Правило:** Arrange, Act, Assert, без коментари `// Arrange`. Действието е **едно** извикване на предмета на теста. Подготовката, по-дълга от няколко реда, отива в помощен метод с име, което казва какво прави (`CreateAsync`, `Add`). Проверките са след действието, не между действия.

```csharp
// Лошо: проверки между действията, подготовка и действие на един ред
var category = Add("figures");
Assert.Single(_repositories.Categories);
await _services.Category.SetActiveAsync(category.Id, false, category.Version, default);
Assert.False(category.IsActive);
await _services.Category.SetActiveAsync(category.Id, true, category.Version + 1, default);

// Добре
var category = Add("figures");

await _services.Category.SetActiveAsync(category.Id, false, category.Version, default);

Assert.False(category.IsActive);
```

Изключение: тест на последователност (първата промяна успява, втората е конфликт, TW3) има две действия, и това е самото поведение.

**Образец:** `CategoryManagementTests`. **Днес:** Да.

### TW5. В тялото на теста няма логика

**Правило:** В тялото на теста няма `if`, `switch`, `foreach`, `for` и `while` с проверки вътре. Различни входове с един и същ резултат са `[Theory]` с `[InlineData]` или `it.each`. Повторение на подготвително действие (20 качвания) е в помощен метод или е изрично `for` само за подготовка, без `Assert` в цикъла. Логика (`if`) е допустима в помощници и имитации (`fakeApi`), не в `[Fact]` и `it`.

```csharp
// Лошо: проверката е в цикъл; при провал не се знае коя итерация е
foreach (var url in urls.Skip(1))
{
    Assert.True(DateTimeOffset.UtcNow - Parse(url) < TimeSpan.FromMinutes(5));
}

// Добре: данните са във [Theory], а при провал се вижда кой вход е лош
[Theory]
[InlineData("Figures")]
[InlineData("two words")]
public async Task Create_WithABadSlug_IsRejectedOnTheSlugField(string slug) { … }
```

**Образец:** `CategoryManagementTests` (`[Theory]`), `categoryValidation.test.ts` (`it.each`). **Днес:** **Не**: цикли с `Assert` вътре в `AdminLoginTests` (два пъти) и `SitemapTests` (D41).

### TW6. Данните идват от помощник, а очакваната стойност е написана в теста

**Правило:** Обектите се създават с помощник (`CatalogData`, `Add`, `category()`), а тестът подава само полето, което проверява (виж [testing.md](testing.md), „Помощници за данни“). Очакваната стойност е **буквална** (`1500`, `"product.slug_taken"`), не се изчислява със същата логика като кода, защото тогава тестът повтаря грешката на кода.

```csharp
// Лошо: очакването се смята като в кода; ако логиката е грешна, и тестът е
Assert.Equal(variant.Price * 2, line.Total);

// Добре
Assert.Equal(3000, line.Total);
```

**Образец:** `CatalogData.Variant` (цена `1500`); `CatalogServiceTests` (`"DRG-001"`). **Днес:** при преглед.

### TW7. Тест на отказана операция проверява и че нищо не е променено

**Правило:** Когато услугата отказва (валидация, конфликт), тестът проверява и **двете**: изключението (тип и `Code` или поле) и че състоянието е същото (нищо не е записано). Без втората проверка тестът не хваща грешка, при която услугата хвърля, но вече е записала половината.

```csharp
// Лошо: само изключението
await Assert.ThrowsAsync<ValidationFailedException>(() => service.CreateAsync(input, default));

// Добре
var error = await Assert.ThrowsAsync<ValidationFailedException>(() => service.CreateAsync(input, default));

Assert.Contains("name", error.Errors.Keys);
Assert.Empty(_repositories.Categories);
```

**Образец:** `CategoryManagementTests.Create_WithoutName_IsRejectedOnTheNameField`; `CatalogServiceTests.PublishingAProductWithoutVariants_IsRejected_AndNothingChanges`. **Днес:** Да.

## 3. Независимост

### TW8. Всеки тест строи собственото си състояние

**Правило:** Тестът не чете нищо, което друг тест е оставил, и не зависи от реда. Unit: ново `FakeRepositoryManager` в конструктора (xUnit създава нов клас за всеки тест). Интеграционен: собствена база (`postgres.CreateDatabaseAsync()`). Frontend: собствен `render` с нов `QueryClient` (`createQueryClient()`), а глобалното се връща (`vi.unstubAllGlobals()` в `afterEach`). В тестовия клас няма `static` поле, което се променя.

```csharp
// Лошо: общо състояние между тестовете
private static readonly FakeRepositoryManager Repositories = new();

// Добре: нов обект за всеки тест
private readonly FakeRepositoryManager _repositories = new();
```

**Образец:** `CategoryManagementTests` и `CatalogConstraintTests`; `categories.test.tsx` (`afterEach`). **Днес:** Да.

## 4. Frontend

### TW9. Елементът се търси така, както го търси потребителят

**Правило:** Ред на предпочитание: `getByRole` с `name` → `getByLabelText` → `getByText` → `getByTestId` (последна възможност, [About Queries](https://testing-library.com/docs/queries/about/)). Не се търси по CSS клас, по `querySelector` и по вътрешно състояние: те не са това, което потребителят вижда и се чупят при преименуване. Ако елементът няма роля или етикет, това е сигнал, че има проблем с достъпността, който се поправя в компонента.

```tsx
// Лошо
const row = screen.getByText("Стари").closest(".category-row");
container.querySelector(".btn-save");

// Добре
screen.getByRole("button", { name: "Активирай „Стари“" });
screen.getByLabelText("Име");
```

**Образец:** `categories.test.tsx` („gives every button of a row a name…“). **Днес:** **Не**: 16 места с `closest(…)` или `querySelector`; плюс `getByTestId` на 10 места (виж [testing.md](testing.md), D28; класовете са D42).

### TW10. Действията са на потребителя: `userEvent`, а `fireEvent` е изключение

**Правило:** Кликът, въвеждането и клавишите се пускат през `userEvent.setup()`, който повтаря фокус, натискане и отпускане. `fireEvent` е за събитие, което потребителят не може да направи (`scroll`, `resize`, зареждане на изображение). Докато пакетът не е инсталиран, новите тестове не умножават `fireEvent` без нужда (виж [testing.md](testing.md)).

```tsx
// Лошо: променя стойността, без фокус и без натискане на клавиши
fireEvent.change(screen.getByLabelText("Име"), { target: { value: "Фигури" } });

// Добре
const user = userEvent.setup();
await user.type(screen.getByLabelText("Име"), "Фигури");
```

**Налага се:** преглед. **Днес:** **Не**: около 125 места с `fireEvent`, пакетът не е инсталиран (D28).

### TW11. Чака се с `findBy…`, а `waitFor` обвива само проверка

**Правило:** Нещо, което се появява асинхронно, се чака с `findBy…`. `waitFor` се ползва само когато няма елемент за чакане (например извикване на мрежата), и в него има **само проверка**, без действия. Отсъствие се проверява с `queryBy…` (връща `null`), а не с `getBy…` в `try/catch`. Без `sleep`, `setTimeout` и реално чакане по часовник (T4).

```tsx
// Лошо: действие вътре в waitFor може да се повтори много пъти
await waitFor(() => {
  fireEvent.click(screen.getByRole("button", { name: "Запази" }));
  expect(screen.getByText("Запазено")).toBeInTheDocument();
});

// Добре
await user.click(screen.getByRole("button", { name: "Запази" }));
expect(await screen.findByText("Запазено")).toBeInTheDocument();
expect(screen.queryByRole("alert")).not.toBeInTheDocument();
```

**Образец:** `categories.test.tsx` (`findByRole`, `queryBy`). **Днес:** Да (в `waitFor` няма `fireEvent`).

### TW12. Проверява се това, което вижда потребителят, не вътрешността

**Правило:** Проверките са за текст, роля, имена, `aria-*` атрибути и адрес. Не се проверява състояние на компонента, props, брой изрисувания или имена на hooks. Снимки на цели страници (`toMatchSnapshot`) са забранени: те се обновяват без четене и не казват какво е важно.

```tsx
// Лошо
expect(wrapper.state("isOpen")).toBe(true);
expect(container).toMatchSnapshot();

// Добре
expect(screen.getByRole("dialog", { name: "Изтриване" })).toBeVisible();
```

**Образец:** `categories.test.tsx`. **Днес:** Да (няма снимки).

### TW13. Мрежата се имитира като истинско API, не като редица от отговори

**Правило:** Имитацията на `fetch` (`vi.stubGlobal('fetch', …)`) се държи като сървър: пази редовете, върши проверките на версията и слъга и връща същите кодове (`409`, `category.slug_taken`). Тя не е `mockResolvedValueOnce`, наредени по ред на извикване: такъв тест се чупи при всяка промяна на броя заявки. Тестът проверява и **какво е изпратено** (методът, адресът, тялото), когато това е поведението (`writes()`).

```ts
// Лошо: зависи от реда и броя на заявките
fetchMock.mockResolvedValueOnce(json(rows)).mockResolvedValueOnce(json(created));

// Добре: малко API в паметта
const api = fakeApi([FIGURES, TOYS]);
…
expect(api.writes()).toEqual([{ method: 'POST', url: BASE, body: { slug: 'figuri', … } }]);
```

**Образец:** `fakeApi` в `categories.test.tsx`. **Днес:** Да за админ панела; за магазина при преглед.

### TW14. Страницата има проверка с `axe`

**Правило:** Всяка страница на магазина има тест с `axe` (`storefront/src/testing/axe.ts`), който проверява, че няма нарушения на WCAG 2.1 AA (цел от [frontend.md](frontend.md), „Достъпност“). `axe` не заменя ръчната проверка с клавиатура и четец на екран, а хваща очевидното. Нарушение не се скрива с изключение без причина.

**Образец:** `storefront/src/accessibility.test.tsx`. **Днес:** Да за магазина; **Не** за админ панела (D43).

## 5. Backend

### TW15. Имитацията е написана на ръка и се държи като истинската

**Правило:** В unit тестовете repository и хранилището за снимки са ръчно написани имитации (`FakeRepositoryManager`, `FakeImageStorage`). Пакет за имитации не се добавя (нов пакет иска одобрение). Имитацията повтаря правилата, които замества: неосвежена версия е конфликт и нищо не се записва; заявките за четене хвърлят `NotSupportedException`, а не връщат празно (T6). Нов repository в `Contracts/` е ново поле в имитацията в същата промяна.

```csharp
// Лошо: имитацията приема всичко и тестът минава, когато не бива
public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;

// Добре: като истинския запис
// Like the real save: a stale version is a conflict and nothing is written.
if (_category.Expected.Any(e => e.Key.Version != e.Value)) { throw new ConflictException(…); }
```

**Образец:** `FakeRepositoryManager.SaveAsync`. **Днес:** Да.

### TW16. Каквото зависи от истинската база, се доказва с истинска база

**Правило:** Ограничения (уникален индекс, външен ключ), миграции, превод на заявки (`ILike`, `Skip`/`Take`) и адреси през целия път се тестват в `Storefront.IntegrationTests` срещу PostgreSQL в контейнер, със собствена база за тест. Резултатът се проверява **отделно** от действието: с нов `DbContext` или с втора заявка, не с обекта, който действието е върнало. Имитират се само външни граници (хранилище за снимки, часовник), а не базата и не услугата.

```csharp
// Лошо: проверка на същия обект в паметта
var product = await services.Product.CreateAsync(input, default);
Assert.Equal(ProductStatus.Published, product.Status);

// Добре: чете се от базата с нов контекст
await using var read = database.CreateContext();
var stored = await read.Products.Include(p => p.Variants).AsNoTracking().SingleAsync();
Assert.Equal(ProductStatus.Published, stored.Status);
```

**Образец:** `CatalogServiceTests`, `CatalogConstraintTests`. **Днес:** Да.

### TW17. Тестът на адрес проверява договора: статус, код на грешка и тип на тялото

**Правило:** За всеки адрес има тест за успеха, за всяка грешка със свой код (`404`, `409`, `400`), за `401` без вход и за `403`, когато има права ([testing.md](testing.md)). При грешка се проверява **`code`** от `ProblemDetails` и че отговорът е `application/problem+json`, никога текстът на съобщението: текстът е за човек и се променя, кодът е договор (B12).

```csharp
// Лошо
Assert.Contains("already used", body);

// Добре
Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
Assert.Equal("category.slug_taken", (await JsonOf(response)).GetProperty("code").GetString());
Assert.True(response.IsProblemJson());
```

**Образец:** `AdminCategoriesApiTests`, `ClosedByDefaultTests`. **Днес:** Да.

### TW18. Тест на едновременност е определен и няма `sleep`

**Правило:** Състезание между два запис се тества чрез версията, не чрез време: две промени с **една и съща** версия, пуснати заедно (`Task.WhenAll`), дават точно един успех и един конфликт, и тестът проверява точно това, не кой е бил първи. Без `Task.Delay` и без повторение, докато се случи.

```csharp
// Лошо: надява се на времето
await Task.Delay(100);

// Добре
var responses = await Task.WhenAll(
    host.SendAsync(HttpMethod.Put, url, Update(created, name: "A"), cookie),
    host.SendAsync(HttpMethod.Put, url, Update(created, name: "B"), cookie));

Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
```

**Образец:** `AdminCategoriesApiTests.TwoUpdatesWithTheSameVersionAtOnce_GiveOneSuccessAndOneConflict`. **Днес:** Да за разделянето; **Не** за времето: `SitemapTests` сравнява с `DateTimeOffset.UtcNow` (D44, нарушение на T4).

## 6. Полезност

### TW19. Новият тест е видян да пада

**Правило:** Преди тестът да се приеме, логиката се чупи нарочно и тестът пада ([testing.md](testing.md), T2). Начин: в кода се обръща условие (`>` в `>=`), маха се проверка или се връща грешна стойност; пуска се само новият тест; кодът се връща. При тест, написан след кода, това е единственият начин да се знае, че проверява нещо. В описанието на pull request-а пише как е направено.

```text
// Пример за описание на pull request
Проверка на теста: в EnsureSlugIsFreeAsync махнах `existing.Id != exceptId`;
Update_WithTheSameSlug_IsAccepted падна със ConflictException; върнах промяната.
```

**Налага се:** преглед (T2). **Днес:** Само при преглед.

### TW20. Проверката е най-точната, която има, и казва какво се разминава

**Правило:** Ползва се проверка, чието съобщение при провал показва разликата: `Assert.Equal(очаквано, получено)` (в този ред), `Assert.Single`, `Assert.Contains`, `Assert.Empty`, `toHaveTextContent`, `toBeInTheDocument`, `toHaveAttribute`. Не `Assert.True(a == b)` и не `expect(x === y).toBe(true)`: при провал се вижда само „False“.

```csharp
// Лошо: при провал пише „Assert.True() Failure. Expected: True, Actual: False“
Assert.True(items.Count == 1);

// Добре: пише кой елемент липсва или какъв е броят
Assert.Equal(slug, Assert.Single(_repositories.Categories).Slug);
```

**Образец:** `CategoryManagementTests.Create_WithAGoodSlug_IsAccepted`. **Днес:** при преглед. `toBe(true)` има на 20 места във frontend тестовете; част проверяват наистина булева стойност и са допустими, но сравнение, опаковано в `toBe(true)`, не е.

## 7. Голям тестов файл

### TW21. Тестовият файл е до 500 реда и се дели по поведение

**Правило:** Границата и начинът на делене са в [testing.md](testing.md) („Голям тестов файл“) и важат и за C# тестове, не само за `.tsx`: делене по поведение, помощниците в споделен файл, тест, копиран във втори файл, е знак, че липсва помощник. В C# тестове делението е на клас `<Област><Тема>Tests` в отделни файлове; общите помощници са в `internal static class` до тях (`CatalogData`).

**Образец:** `CatalogConstraintTests` + `CatalogData` (общ помощник). **Днес:** **Не**: C# тестови файлове над 500 реда: `ProductManagementTests` 815, `AdminProductsApiTests` 796, `ProductImageManagementTests` 529, `AdminProductImagesApiTests` 516 (D40). Frontend: D24.

## Връзки с другите правила

Правилата тук допълват, не повтарят. При противоречие важи редът от [README.md](README.md).

| Тук                                                                                                        | Допълва / се основава на                                                                | Разлика                                                           |
| ---------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| TW1–TW3 (име, обхват)                                                                                      | [testing.md](testing.md) (кога, къде), [testing.md](testing.md) („Нива и общи правила“) | testing.md казва къде е файлът, тук как се казва тестът           |
| TW5 (без логика)                                                                                           | T4 в [testing.md](testing.md) (без `sleep`, без случайност)                             | T4 е за време и случайност, TW5 за `if` и цикли                   |
| TW8 (независимост)                                                                                         | [testing.md](testing.md) („Правила за всички тестове“), T4                              | Същото; тук е как се постига във всеки вид тест                   |
| TW9–TW10 (заявки, `userEvent`)                                                                             | [testing.md](testing.md) („Какво днес се отклонява“), D28                               | Там е отклонението, тук е правилото                               |
| TW11 (`waitFor`)                                                                                           | T4                                                                                      | T4 забранява `sleep`, TW11 казва с какво се чака                  |
| TW15 (имитация)                                                                                            | T5, T6                                                                                  | T5–T6 са границата на имитацията, TW15 как е написана             |
| TW16–TW17 (интеграционни, договор)                                                                         | [testing.md](testing.md) (таблица „Кога“), B12 в [backend.md](backend.md)               | testing.md казва, че има интеграционен тест, тук как се проверява |
| TW19 (видян да пада)                                                                                       | T2, [testing.md](testing.md) („Допълнително за ИИ“)                                     | T2 е правилото, TW19 как се прави                                 |
| TW21 (голям файл)                                                                                          | [testing.md](testing.md), F7 в [frontend.md](frontend.md), D24                          | Същата граница; тук е и за C#                                     |
| Тестове в [react-golden-rules.md](react-golden-rules.md), [csharp-golden-rules.md](csharp-golden-rules.md) | RC28 (четири случая на екрана)                                                          | Какво се тества за екран е в RC28, как се пише тестът е тук       |

Правилата за архитектура и данни (миграции, `seed`, схема) са в [architecture-and-data-rules.md](architecture-and-data-rules.md). Изолацията на базата в тестовете е в TW16 и `PostgresFixture` (всеки тест създава своя база).

## Ред в дълга от този файл

- TW5: цикли с `Assert` вътре в теста (D41).
- TW9: класове и `querySelector` вместо роля и етикет (D42).
- TW14: няма проверка с `axe` в админ панела (D43).
- TW18: `SitemapTests` сравнява с `DateTimeOffset.UtcNow` (D44).
- TW21: C# тестови файлове над 500 реда (D40).
