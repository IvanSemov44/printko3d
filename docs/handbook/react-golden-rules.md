# Златни правила: React и TypeScript

Как се пише един компонент, един hook и един тип. [frontend.md](frontend.md) казва **къде** стои кодът и колко голям е; този файл казва **как изглежда**. Правилата не повтарят другите документи, а ги допълват и сочат към тях (таблицата в края показва връзките).

**Формат.** Всяко правило е в една рамка: **правило** (отговор „да“ или „не“) → **лош пример** → **добър пример** → **образец** от репото или от документацията. Колоната „Днес“ казва дали кодът го следва; „не“ е ред в [tech-debt.md](../tech-debt.md). Номерата са `RC1`, `RC2`… (React code), за да не се бъркат с `F` (подредба), `T` (тестове) и `R` (рефакториране).

**Версии.** React 19, TypeScript със `strict`, TanStack Query 5, React Router 8. Проектът **не ползва React Compiler** (`@vitejs/plugin-react` без компилатор), затова правилата за `useMemo` и `useCallback` са за ръчна работа. добави библиотеката

**Източници.** [Thinking in React](https://react.dev/learn/thinking-in-react), [You Might Not Need an Effect](https://react.dev/learn/you-might-not-need-an-effect), [Rules of Hooks](https://react.dev/reference/rules/rules-of-hooks), [Rules of React](https://react.dev/reference/rules), [Rendering Lists](https://react.dev/learn/rendering-lists), [Preserving and Resetting State](https://react.dev/learn/preserving-and-resetting-state), [useMemo](https://react.dev/reference/react/useMemo) и [useCallback](https://react.dev/reference/react/useCallback) на react.dev; [Queries](https://tanstack.com/query/latest/docs/framework/react/guides/queries) на TanStack Query; [Narrowing](https://www.typescriptlang.org/docs/handbook/2/narrowing.html) и [`satisfies`](https://www.typescriptlang.org/docs/handbook/release-notes/typescript-4-9.html) на TypeScript; [Bulletproof React](https://github.com/alan2207/bulletproof-react/blob/master/docs/project-structure.md) само за подредбата по функционалности.

**Как се налага.** Автоматично (ESLint, `eslint.config.js`): `strictTypeChecked`, `no-explicit-any`, `ban-ts-comment`, `no-console` и правилата на `eslint-plugin-react-hooks` (RC1, RC14–RC15, RC20, RC24). Останалото е при преглед; 16c може да добави още.

## 1. Компонент

### RC1. Компонентът е чиста функция на props и състояние

**Правило:** Рисуването не променя нищо извън компонента: няма запис в променлива извън функцията, в DOM или в мрежата. Същите props и състояние дават същата разметка ([Keeping Components Pure](https://react.dev/learn/keeping-components-pure)).

```tsx
// Лошо: променя променлива извън компонента при всяко рисуване
let shown = 0;
function Card({ name }: { name: string }) {
  shown += 1;
  return (
    <p>
      {name} #{shown}
    </p>
  );
}

// Добре: данните идват от props
function Card({ name, position }: { name: string; position: number }) {
  return (
    <p>
      {name} #{position}
    </p>
  );
}
```

**Образец:** `ProductCard.tsx`. **Днес:** Да. **Налага се:** `react-hooks` (частично) и преглед.

### RC2. Props са малко и типизирани, с именуван интерфейс

**Правило:** Props на всеки компонент са интерфейс или тип с име `<Компонент>Props`, а компонентът е обикновена функция, не `React.FC`. Компонент с повече от около 6 props се дели или получава `children`.

```tsx
// Лошо: непосочен тип, твърде много props
function Field(props: any) {
  /* … */
}

// Добре
interface FormFieldProps {
  id: string;
  name: string;
  label: string;
  error?: string | undefined;
  children: (control: FieldControlProps) => ReactNode;
}
export function FormField({
  id,
  name,
  label,
  error,
  children,
}: FormFieldProps) {
  /* … */
}
```

**Образец:** `admin/src/components/FormField.tsx`. **Днес:** Да (няма `React.FC`).

### RC3. Логиката е в hook, разметката е в компонента

**Правило:** Компонентът чете hook и рисува. Заявка, мутация, избор от адреса и по-сложно изчисление са в `use*.ts` или в чиста функция, не в тялото на компонента ([frontend.md](frontend.md), F10).

```tsx
// Лошо: компонентът знае ключ, заявка и инвалидиране
function List() {
  const [items, setItems] = useState<Item[]>([]);
  useEffect(() => {
    fetch("/api/v1/items")
      .then((r) => r.json())
      .then(setItems);
  }, []);
  /* … */
}

// Добре
function List() {
  const items = useAdminCategories();
  /* само разметка */
}
```

**Образец:** `useCategories.ts` и `CategoryListPage.tsx`. **Днес:** Да.

### RC4. Компонент не се дефинира в друг компонент

**Правило:** Помощен компонент е на най-високо ниво във файла или в свой файл (виж [frontend.md](frontend.md), F8). Вътрешният компонент е нов тип при всяко рисуване и губи състоянието си ([Preserving and Resetting State](https://react.dev/learn/preserving-and-resetting-state)).

```tsx
// Лошо
function Page() {
  function Row({ name }: { name: string }) {
    return <li>{name}</li>;
  }
  return (
    <ul>
      {names.map((n) => (
        <Row key={n} name={n} />
      ))}
    </ul>
  );
}

// Добре: Row е извън Page
function Row({ name }: { name: string }) {
  return <li>{name}</li>;
}
function Page() {
  /* … */
}
```

**Образец:** `CategoryForm` и `CategoryFormPage` в един файл. **Днес:** Да.

### RC5. Само именувани експорти

**Правило:** Компонент и hook се експортират по име (`export function X`), няма `export default`. Така внасянето е едно и също име навсякъде и преименуването намира всички места.

**Образец:** всички файлове в `features/`. **Днес:** Да (няма `export default` извън конфигурациите на Vite).

## 2. Състояние

### RC6. Състоянието е най-ниско, където се ползва

**Правило:** Състояние, което ползва само един компонент, стои в него. Вдига се нагоре само когато го ползват двама братя ([Sharing State Between Components](https://react.dev/learn/sharing-state-between-components)). Нещо, което оцелява при смяна на екрана или трябва да се споделя по адрес (страница, филтър), е в адреса (`useSearchParams`), не в `useState`.

**Образец:** `useSelectedVariant.ts` (избраният вариант е в адреса, параметър `variant`). **Днес:** Да.

### RC7. Не пази това, което може да се изчисли

**Правило:** Ако стойността се изчислява от props или друго състояние, изчислява се при рисуване. Не се пази в `useState` и не се синхронизира с `useEffect` ([Thinking in React](https://react.dev/learn/thinking-in-react), стъпка „минималното състояние“).

```tsx
// Лошо: две състояния, които могат да се разминат
const [items, setItems] = useState<Item[]>([]);
const [count, setCount] = useState(0);
useEffect(() => {
  setCount(items.length);
}, [items]);

// Добре
const [items, setItems] = useState<Item[]>([]);
const count = items.length;
```

**Образец:** `selectVariant(variants, param)` в `useSelectedVariant.ts` извежда варианта от адреса. **Днес:** Да.

### RC8. Данните от сървъра са в TanStack Query, не в `useState`

**Правило:** Нищо, което идва от API, не се копира в `useState`. Правилата за ключове и инвалидиране са във [frontend.md](frontend.md) (F10–F12).

**Образец:** `useCategories.ts`. **Днес:** Да. Изключение, което е допустимо: началните стойности на формата се взимат от заредения обект (копие за редакция), защото потребителят ги променя.

### RC9. Нулиране на състоянието се прави с `key`, не с ефект

**Правило:** Когато формата трябва да започне отначало (друг продукт, повторно зареждане), на компонента се слага `key`, който се променя. Не се пише `useEffect`, който нулира полетата ([You Might Not Need an Effect](https://react.dev/learn/you-might-not-need-an-effect), „Resetting all state when a prop changes“).

```tsx
// Лошо
useEffect(() => {
  setValues(initial);
}, [productId]);

// Добре
<ProductForm key={productId} initial={initial} />;
```

**Образец:** `ProductFormPage.tsx`, `key={`${id ?? 'new'}:${String(generation)}`}`. **Днес:** Да.

### RC10. Състояние с взаимно изключващи се случаи е обединение, не няколко булеви

**Правило:** Ако две булеви стойности не могат да са истина заедно (зарежда се / грешка / готово), това е един тип с признак, не `isLoading` и `isError` като отделни състояния ([Narrowing](https://www.typescriptlang.org/docs/handbook/2/narrowing.html)).

```ts
// Лошо: възможно е и двете да са true
const [isLoading, setIsLoading] = useState(false);
const [isError, setIsError] = useState(false);

// Добре
type Upload =
  | { state: "idle" }
  | { state: "sending" }
  | { state: "failed"; message: MessageKey };
```

Заявките на TanStack Query вече са такова обединение (`status`: `pending`, `error`, `success`), затова за тях се ползва неговото, не се прави второ.

**Образец:** `status` на заявките на TanStack Query. **Днес:** при преглед; `SettingsPage.tsx` държи `conflict`, `saved` и `formError` като отделни състояния и е кандидат за проверка при делене (D21).

## 3. `useEffect`

### RC11. `useEffect` е само за синхронизация с външна система

**Правило:** Ефектът е за нещо извън React: DOM API (фокус, `showModal`, скрол), абонамент, таймер, запис на CSS променлива. Ако няма външна система, ефект не е нужен ([You Might Not Need an Effect](https://react.dev/learn/you-might-not-need-an-effect)).

Случаи, в които ефект **не** е нужен, и какво се прави:

| Искаш да…                                   | Не е ефект. Прави                                    |
| ------------------------------------------- | ---------------------------------------------------- |
| Изчислиш стойност от props или състояние    | Изчисли я при рисуване (RC7)                         |
| Нулираш състояние при смяна на props        | `key` (RC9)                                          |
| Реагираш на клик, изпращане, избор          | Обработчик на събитието                              |
| Заредиш данни                               | `useQuery` (RC8, [frontend.md](frontend.md), F10)    |
| Изпратиш данни след действие на потребителя | `useMutation` в обработчика                          |
| Предадеш данни на родителя след промяна     | Извикай `onChange` в обработчика, вдигни състоянието |

**Допустим ефект (образци):**

| Файл                                   | Външната система                                   |
| -------------------------------------- | -------------------------------------------------- |
| `ConfirmDialog.tsx`                    | `dialog.showModal()` / `close()`                   |
| `VariantForm.tsx`, `ImageEditForm.tsx` | Фокус на първото поле при показване (`focusField`) |
| `ImagesSection.tsx`                    | Фокус на бутона след преместване на снимка         |
| `CategoryProducts.tsx`                 | Фокус и скрол към заглавието при нова страница     |
| `StoreSettingsProvider.tsx`            | CSS променливи на документа (`applyTheme`)         |

**Днес:** Да. Пет ефекта, всички за външна система.

### RC12. Ефект, който се абонира, се почиства

**Правило:** Ефект с абонамент, таймер или слушател връща функция за почистване. Ефект без почистване е допустим само за еднократно действие (фокус).

**Днес:** Да: нито един ефект не се абонира за събитие или таймер, затова няма какво да се почиства.

## 4. Hooks

### RC13. Hook се вика само на най-високо ниво и само от компонент или hook

**Правило:** Без hook в условие, цикъл, вложена функция, обработчик или след ранен `return` ([Rules of Hooks](https://react.dev/reference/rules/rules-of-hooks)). Hook не се подава като prop.

**Налага се:** `react-hooks/rules-of-hooks` (грешка). **Днес:** Да.

### RC14. Името `use…` е само за функция, която вика hooks

**Правило:** Функция, която не вика hooks, е с обикновено име (`selectVariant`, `buildCategoryTree`), не `useSelectVariant` ([Reusing Logic with Custom Hooks](https://react.dev/learn/reusing-logic-with-custom-hooks)). Hook споделя логика, не състояние: две извиквания имат две независими състояния.

**Образец:** `selectVariant` (чиста) срещу `useSelectedVariant` (hook). **Днес:** Да.

### RC15. Зависимостите на ефекта са пълни и линтерът не се заглушава

**Правило:** Масивът със зависимости съдържа всичко, което ефектът чете. Няма `eslint-disable` за `react-hooks/exhaustive-deps`; ако зависимостта пречи, ефектът се преработва (виж RC11).

**Налага се:** `react-hooks/exhaustive-deps`. **Днес:** Да (няма `eslint-disable` в кода).

### RC16. `useMemo` и `useCallback` само с причина в коментар

**Правило:** Използват се, когато резултатът е зависимост на друг hook или се подава на компонент, обвит в `memo`, или изчислението е измерено като скъпо (над около 1 ms, [useMemo](https://react.dev/reference/react/useMemo)). Причината е в коментар на един ред. Без измерване и без причина не се слагат.

```ts
// Лошо
const label = useMemo(() => `${first} ${last}`, [first, last]);

// Добре: простото изчисление е без useMemo
const label = `${first} ${last}`;

// Добре: стабилна функция, защото е зависимост на ефект (с коментар)
// Stable on purpose: the effect below lists it as a dependency.
const announce = useCallback(() => {
  /* … */
}, []);
```

**Днес:** **Не**: три места без коментар за причината (`useRouteChange`, `useSelectedVariant`, `useT` в `store-settings/useStoreSettings.ts`).

## 5. Props и композиция

### RC17. Съдържанието се подава като `children` или като функция, не като дълъг списък от props

**Правило:** Когато компонентът обвива променливо съдържание, то е `children`; когато обвивката трябва да даде данни на съдържанието, `children` е функция (render prop). Не се добавя нов prop за всеки вариант на съдържанието.

```tsx
// Лошо
<Field label="Име" inputType="text" inputValue={v} onInputChange={set} errorText={e} hintText={h} />

// Добре
<FormField id="name" name="name" label={t('…')} error={e}>
  {(control) => <input {...control} value={values.name} onChange={…} />}
</FormField>
```

**Образец:** `FormField.tsx`. **Днес:** Да.

### RC18. Prop не се пренася през повече от две нива

**Правило:** Данни, нужни на много дълбоки компоненти (настройките на магазина), са в контекст с hook за четене. Данни за един-два нива се подават като props ([Passing Data Deeply with Context](https://react.dev/learn/passing-data-deeply-with-context)).

**Образец:** `StoreSettingsContext.ts` + `useStoreSettings.ts`. **Днес:** Да.

### RC19. Булев prop не сменя целия вид на компонента

**Правило:** Ако булев prop сменя повече от един детайл на изгледа, компонентът се дели на два или получава именуван вариант (`'compact' | 'full'`), не флаг. Булев prop за едно нещо (`disabled`, `busy`) е допустим.

**Днес:** при преглед.

## 6. TypeScript

### RC20. Без `any`; за непознато се ползва `unknown` и стесняване

**Правило:** `any` не се пише. Стойност с неизвестна форма е `unknown` и се стеснява с проверка (`typeof`, `instanceof`, четец от `decode.ts`) преди ползване ([unknown](https://www.typescriptlang.org/docs/handbook/release-notes/typescript-3-0.html)).

```ts
// Лошо
catch (e: any) { show(e.message); }

// Добре
catch (error) {
  if (error instanceof ApiError) { show(messageFor(error)); }
}
```

**Налага се:** `@typescript-eslint/no-explicit-any` (грешка). **Образец:** `readObject` в `decode.ts`. **Днес:** Да.

### RC21. `as` не се слага върху данни от мрежата

**Правило:** `as` е позволено само за `as const`, за разширяване на тип към по-широк (`(LIST as readonly string[]).includes(x)`) и в четец на `decode.ts` след проверка, с коментар. Върху отговор от `fetch` е забранено (виж [frontend.md](frontend.md), F13 и [pitfalls.md](pitfalls.md), №16).

**Образец:** `adminCategories.ts` (четци). **Днес:** **Не**: `client.ts` кастира четири отговора (D17).

### RC22. Без `!`, `@ts-ignore` и `@ts-nocheck`

**Правило:** Няма оператор `!` (non-null assertion), `@ts-ignore` и `@ts-nocheck`. `@ts-expect-error` само с обяснение. Това съвпада с [CLAUDE.md](../../CLAUDE.md) („без `@ts-ignore` без обяснение“) и е по-строго в едно: ESLint вече забранява `@ts-ignore` изцяло (`ban-ts-comment`), затова обяснено потискане се пише със `@ts-expect-error`. Липсващата стойност се обработва: ранен `return`, `?? стойност` или проверка.

**Налага се:** `ban-ts-comment` (за `ts-ignore`). **Днес:** Да (няма нито един).

### RC23. Таблица „стойност → текст“ е с `satisfies Record<…>`

**Правило:** Съответствие от обединение от низове към ключ на превод се пише `as const satisfies Record<Тип, MessageKey>`, така че нова стойност на обединението без текст не се компилира ([`satisfies`](https://www.typescriptlang.org/docs/handbook/release-notes/typescript-4-9.html)).

```ts
// Лошо: Record<string, string> приема всичко
const STATUS_KEYS: Record<string, string> = { Draft: "products.status.Draft" };

// Добре
export const STATUS_KEYS = {
  Draft: "products.status.Draft" /* … */,
} as const satisfies Record<ProductStatus, MessageKey>;
```

**Образец:** `productLabels.ts`. **Днес:** Да.

### RC24. Типовете на API идват от `schema.d.ts`

**Правило:** Тип на отговор или заявка не се пише на ръка, а се взема от `components['schemas'][…]` и се стеснява с `Omit`/`&` само за разликата (например `integer` като `number`). Виж [frontend.md](frontend.md), F14.

**Образец:** `AdminCategory` в `adminCategories.ts`. **Днес:** Да.

## 7. Именуване

### RC25. Имена

| Какво                        | Правило                                                                                       | Образец                             |
| ---------------------------- | --------------------------------------------------------------------------------------------- | ----------------------------------- |
| Компонент                    | `PascalCase`, името на файла е същото                                                         | `CategoryListPage.tsx`              |
| Hook                         | `useXxx`, файл `useXxx.ts`                                                                    | `useAdminCategories.ts`             |
| Чиста функция и помощен файл | `camelCase`                                                                                   | `categoryTree.ts`, `slug.ts`        |
| Обработчик в компонента      | `handleXxx` (`handleSubmit`, `handleMove`)                                                    | `CategoryListPage.tsx`              |
| Prop-обратно извикване       | `onXxx` (`onReload`, `onConfirm`)                                                             | `ConfirmDialog.tsx`                 |
| Константа                    | `UPPER_SNAKE_CASE` за стойност на модула                                                      | `CATEGORIES_KEY`, `NAME_MAX_LENGTH` |
| Тип на props                 | `<Компонент>Props`                                                                            | `FormFieldProps`                    |
| Булева стойност              | `isXxx`, `hasXxx`, `canXxx`; изключение: име, което повтаря HTML атрибут (`open`, `disabled`) | `isDemo`                            |

**Днес:** обработчици и файлове: Да. Булеви: **Не**: `busy` (4 места), `failed`, `full`, `noindex`, `healthOk`.

## 8. Списъци и условно рисуване

### RC26. Ключът идва от данните

**Правило:** `key` е стабилен идентификатор от данните (`id`, `sku`), не индекс на масива и не `Math.random()` ([Rendering Lists](https://react.dev/learn/rendering-lists)).

```tsx
// Лошо
{
  items.map((item, i) => <Row key={i} item={item} />);
}

// Добре
{
  items.map((item) => <Row key={item.id} item={item} />);
}
```

**Образец:** `ProductGallery.tsx`, `VariantPicker.tsx`. **Днес:** Да (няма ключ по индекс).

### RC27. Условното рисуване не показва `0`, `false` или празен низ по погрешка

**Правило:** Условие с число пред `&&` се сравнява изрично (`count > 0 && …`) или се ползва тернарен оператор. Нееднакви случаи (`pending`, `error`, празно, данни) се разделят с ранен `return` (RC28), не с вложени тернарни.

```tsx
// Лошо: при count === 0 на екрана се появява "0"
{
  count && <Badge count={count} />;
}

// Добре
{
  count > 0 ? <Badge count={count} /> : null;
}
```

**Днес:** при преглед.

## 9. Екран: зареждане, грешка, празно

### RC28. Всеки екран с данни обработва четири случая, в този ред

**Правило:** Екранът, който чете `useQuery`, обработва по ред: зарежда се (`isPending`), грешка (`isError`), празно (данните са, но са без елементи) и съдържание, с ранен `return` за първите три ([Queries](https://tanstack.com/query/latest/docs/framework/react/guides/queries): „първо pending, после error, после данни“). Празното не е грешка: има собствен текст и, когато има смисъл, действие.

```tsx
// Добре
if (categories.isPending) return <LoadingStatus />;
if (categories.isError)
  return <ErrorWithRetry onRetry={() => void categories.refetch()} />;
if (categories.data.length === 0) return <Empty />;
return <List items={categories.data} />;
```

**Образец:** `CategoryListPage.tsx` (`isPending`, `isError`, после съдържание). **Днес:** при преглед. Проверката да не е само „има ли ранен return“, а има ли и трите текста в `locales/bg.json`.

### RC29. Спиращо зареждане и фонова презареда са различни

**Правило:** Пълният текст „зарежда се“ е за `isPending` (още няма данни). За презареда с вече налични данни (`isFetching`, `isPlaceholderData`) се показва лек индикатор или нищо, не се губят данните от екрана; грешката на презареда не прави екрана празен ([Queries](https://tanstack.com/query/latest/docs/framework/react/guides/queries): `status` за „може ли да се покаже“, `fetchStatus` за „мрежова дейност“).

**Образец:** `CategoryProducts.tsx` (`isPlaceholderData` при смяна на страница). **Днес:** Да.

### RC30. Грешката на API се превежда в текст с именувана функция

**Правило:** Пренасянето на `ApiError` (`status`, `code`) към ключ на текст е функция с име `messageFor…(error)`, не верига от условия вътре в JSX. Проверката е по `instanceof ApiError`, после по `status` и `code`; никога по текста на съобщението. Формата може да има `handleError`, който освен текст задава грешка на поле и фокус ([frontend.md](frontend.md), F18, F19). Когато една и съща функция се появи в трети файл, се изнася (R2 в [refactoring.md](refactoring.md)).

```tsx
// Лошо: условия в разметката и сравнение по текст
{
  error && (error.message.includes("409") ? <p>…</p> : <p>…</p>);
}

// Добре
function messageForActionError(error: Error): MessageKey {
  return error instanceof ApiError && error.status === 409
    ? "categories.error.changedByOther"
    : "categories.error.actionFailed";
}
```

**Образец:** `CategoryListPage.tsx`. **Днес:** Да: `messageFor…` има в пет файла. Наблюдение, не нарушение: `messageForActionError` е написана поотделно в три файла със същата форма и различни ключове; ако се появи четвърта, R2 изисква общ помощник.

## Връзки с другите правила

Правилата тук допълват, не повтарят. При противоречие важи редът от [README.md](README.md).

| Тук                                    | Допълва / се основава на                                                 | Разлика                                                                             |
| -------------------------------------- | ------------------------------------------------------------------------ | ----------------------------------------------------------------------------------- |
| RC3 (логика в hook)                    | [frontend.md](frontend.md) F10, F17, вид `use*.ts`                       | F казва къде стои hook-ът, RC3 че логиката не е в разметката                        |
| RC4 (без вложен компонент)             | F8                                                                       | Същото правило; тук е с обяснение защо                                              |
| RC7 (изчислимо не е състояние)         | F15                                                                      | F15 сочи насам                                                                      |
| RC8 (сървърни данни)                   | F10–F12                                                                  | F дава ключовете и инвалидирането, RC8 забраната за `useState`                      |
| RC21 (`as`)                            | F13, [pitfalls.md](pitfalls.md) №16                                      | Същото правило; тук са разрешените изключения                                       |
| RC24 (типове от OpenAPI)               | F14                                                                      | Същото; RC24 казва как се стеснява                                                  |
| RC28–RC29 (зареждане, грешка, празно)  | F18 (сървърът е съдия), [CLAUDE.md](../../CLAUDE.md) (достъпност)        | RC28 е единственото правило за четирите случая                                      |
| RC30 (превод на грешка)                | F18, F19, R2 в [refactoring.md](refactoring.md)                          | F18–F19 са за грешките по поле във формата, RC30 за превода на `ApiError` към текст |
| Тестове на компоненти (заявки по роля) | [testing.md](testing.md), [test-writing-rules.md](test-writing-rules.md) | Не се повтарят тук                                                                  |

Форми (`FormField`, `useFormValues`), пари (`formatMoney`), текстове (`t()`) и достъпност са във [frontend.md](frontend.md), F16–F24, и тук не се повтарят.

## Ред в дълга от този файл

- RC16: три `useMemo`/`useCallback` без коментар (D36).
- RC21: `client.ts` с `as` (вече D17).
- RC25: булеви без `is`/`has` (D35).
