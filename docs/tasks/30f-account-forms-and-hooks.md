## Задача

Подреди функционалността `account` в магазина по правилата на `frontend.md`: заявките за промяна са зад hooks (F10), стойностите на формите са в `useFormValues` (F17), фокусът при грешка е през `focusField` (F19), а `AccountPage.tsx` е разделен на файлове, всеки с едно нещо (F6). Поведението не се променя.

## Контекст

- Правила: [frontend.md](../handbook/frontend.md) („Видове файлове“, F4, F6, F10, F17, F19), [refactoring.md](../handbook/refactoring.md), решение [0028](../decisions/0028-storefront-shared-components-and-exceptions.md).
- Задачата идва от разговор за [AccountPage.tsx](../../src/web/storefront/src/features/account/AccountPage.tsx) (задача 30, решение [0039](../decisions/0039-customer-accounts-password-and-social-login.md)). Във файла са четири компонента: `PasswordForm`, `ResendConfirmation`, `Profile` и `AccountPage`. Три от тях викат `useMutation` директно с функция от `@storefront/shared`, а формата държи по един `useState` на поле и мести фокуса на ръка.
- Същият модел (`useMutation` в компонента) има и в `LoginPage`, `RegisterPage`, `ForgotPasswordPage`, `ResetPasswordPage`, `ConfirmEmailPage`, `ExternalConnections` (`account`), `TestPaymentPage` (`checkout`) и `MyOrderPage` (`orders`). `useState('')` за поле има в `AccountPage`, `LoginPage`, `ForgotPasswordPage`, `ResetPasswordPage` и `MyOrderPage`.
- Таблицата във `frontend.md` пише „Днес: Да“ за F10 и F17, а това не е вярно за магазина. Проверката на F10 („в компонент няма `useEffect`, който вика API“) не хваща `useMutation` в компонента.
- `useFormValues` и `focusField` са в [admin/src/components/useFormValues.ts](../../src/web/admin/src/components/useFormValues.ts) и се ползват от седем файла на панела. Магазинът няма откъде да ги вземе. По F4 при второто ползване (магазинът) отиват в `shared`.
- Образец за hooks на мутациите: [useCartMutations.ts](../../src/web/storefront/src/features/cart/useCartMutations.ts).
- `accountValidation.ts` има `firstInvalid`, който връща първото поле с грешка. Той остава, а `focusField` замества само ръчното местене на фокуса.

## Критерии за приемане

**30f-1: записът (само документи)**

- Има ред в `tech-debt.md` (следващ свободен номер, D77), раздел „Задача 30“: F10 и F17 не се спазват в магазина, с изброените файлове, цена С, начин на поправка (30f-2, 30f-3 и следващи части по функционалност), статус „за плащане“.
- Колоната „Днес“ на F10 и F17 във `frontend.md` е „**Не** в магазина“ с препратка към реда. На F19 също, ако планът потвърди, че магазинът мести фокуса на ръка.

**30f-2: `useFormValues` и `focusField` в `shared`**

- Файлът е преместен с `git mv` в `shared/src/` и е изнесен от `shared/src/index.ts`. Седемте файла на панела го внасят от `@storefront/shared`. В `admin/src/components/` не остава копие.
- Има тест в `shared` за `useFormValues` (една промяна не губи другите полета) и за `focusField` (фокусира полето по име; без такова поле не хвърля).
- Тестовете на панела са същите, без промяна на очакванията.
- Таблицата на `shared` в `architecture.md` и образецът на F17 във `frontend.md` сочат новото място.

**30f-3: `AccountPage` на части**

- `PasswordForm.tsx`, `ResendConfirmation.tsx` и `Profile.tsx` са в свои файлове в `features/account/`. `AccountPage.tsx` само избира между зареждане, грешка, пренасочване и профил.
- `useMutation` за смяна на парола, ново писмо за потвърждение и изход са в `useAccountMutations.ts` (`useChangePassword`, `useResendConfirmation`, `useSignOut`). Никой компонент от тези файлове не вика `useMutation` директно.
- `PasswordForm` ползва `useFormValues` и `focusField` от `shared`. Няма `useState` за стойност на поле.
- Изходът работи както преди: навигация към `/`, презаредена количка, изчистен купувач чак когато страницата изчезне. Това се проверява от съществуващ тест или от нов, ако днес няма.
- Тестовете в `AccountPages.test.tsx` и `AccountEmailPages.test.tsx` минават без промяна на очакванията.
- Всеки файл е под 250 реда (F5).

## Граници

- Не се пипат `LoginPage`, `RegisterPage`, `ForgotPasswordPage`, `ResetPasswordPage`, `ConfirmEmailPage`, `ExternalConnections`, `TestPaymentPage` и `MyOrderPage`. Те остават в реда D77 за следващи части, по една функционалност на част.
- Не се обединяват двата `FormField` (`admin/src/components` и `storefront/src/components`). Те са почти еднакви, но обединяването е отделно решение.
- Не се променят текстовете, видът, адресите на API и функциите в `shared/src/api/account.ts`.
- Нови пакети: не.
- Дълг: добавя D77 (30f-1) и го намалява с `AccountPage` (30f-3). Редът се затваря чак когато всички изброени файлове са поправени.
- Размер: 30f-1 е два файла. 30f-2 е над пет файла само заради смяната на внасянията в панела (механично, `git mv` и `import`), затова планът го казва и иска одобрение. 30f-3 е пет файла (`AccountPage`, три нови компонента, `useAccountMutations`) плюс тест, ако липсва. Всяка част е отделен pull request в този ред.

## Как се проверява

- Frontend (от `src/web`): `npm run format:check`, `npm run lint`, `npm test`, `npm run build`.
- Backend не се пипа.
- Ръчно в `docker compose up --build`:
  - 30f-2: формата на категорията и настройките в панела записват и фокусират първото невалидно поле както преди;
  - 30f-3: на `/account` смяна на парола (грешна, къса, вярна), ново писмо за потвърждение (вижда се в Mailpit) и изход, който води към началната страница с празна количка.

## Преди да пишеш код

Дай кратък план за частта, която започваш: кои файлове се местят или създават и какво остава. Предположения, които да потвърдиш:

- в `shared` файлът се казва `useFormValues.ts` и стои в корена на `shared/src/`, до `useApiHealth.ts`;
- изходът остава в `Profile`, а не става отделен компонент;
- `firstInvalid` остава в `accountValidation.ts`.
