# Localization plumbing

Where strings come from, how a key is derived, and how the culture is chosen.

The rules a screen writes by — a key missing everywhere renders as itself, entity names
capitalized inside a label, menu entries and titles in Title Case, the blank `Label` — stay in
[intentional-ui.md](../intentional-ui.md). Key composition itself is
[metadata.md](../metadata.md). The record is in
[intentional-ui-cases-shell.md](../intentional-ui-cases-shell.md) under *Localization*; a rule marked
*(cases)* has its story there.

---

Strings resolve through `StringManager` (`NSail.Localization`): **`IStringSource`s feed the
manager**, merged in registration order, last wins — kits, then app, then (future, unbuilt) a
database overlay. Each module ships `strings.json` (base language, English) + `strings.{lang}.json`
overlays (flat key→text, at the project root) as embedded resources, registered with
`AddStringsFromAssembly()`.

**A word a PRODUCT lays over a kit's is a string registered after the kit's. A nav entry an
INSTALL renamed is not a string at all**: the shop's word rides the menu item itself
(`NavMenuItem.Label`, per language, stored with the install's menu arrangement —
[navigation.md](navigation.md)), so nothing reaches the catalogs and nothing needs invalidating.

**Where each derived key comes from.** Fields derive from their binding expression; `NsTh`/`NsTd`
from `For="() => context.Name"` (expressions are inspected, never executed; that shared key also
pairs a header with its cells for per-column visibility); `NsPartial.GetTitle()` from `GetType()`
(`Directory.PartiesPage.Title`, rendered with `<NsText As="Title">@GetTitle()</NsText>`);
`NsPageLink TPage=` from the destination page's `Title`; `NsPartial.GetAction(Method, arg)` from
the method name; buttons without content from `Common.{As}`.

- **`GetAction` needs the bare method group, never a lambda** — the name comes from
  `[CallerArgumentExpression]`, and a lambda captures the whole lambda text.
- **Pages** use `Translate(key, fallback?)`: `{Area}.{Page}.{key}`, then the key globally, then
  the fallback (or the key).
- **Enum values** translate as `{Area}.{Enum}.{Value}` (`StringManager.Translate(Enum)`, usable
  from services). Selects take `Items="@(GetItems<PartyKind>())"` (`Option<T>` values with
  translated texts plus a leading null labeled `Common.All`; `all: false` for forms); cells use
  `Translate(row.Kind)`.
- **A type's bare key is its concept label**: `Directory.OrganizationRef` → "Organization".
  Item-bearing fields fall back to `KeyFor(typeof(TItem))` when the expression-derived key has no
  translation — **one label string per concept**; message-scoped label strings exist only where
  the context differs. A field bound to a page-local value labels itself with no `Label` at all.
  **A refusal reads the same label**: `BusinessProblem`'s entity slot carries the concept key and
  `Translate(Issue)` resolves it before filling `{entity}`, falling back to the key's last segment
  (types.md). An entity a problem can name owes its bare key in `en` and `es`, and
  `ProblemEntityStringTests` fails the build without it.
- **A search box hints itself.** `NsSearchField` falls back to `Common.Search`; a caller wanting
  different words sets the inherited `Placeholder`.
- **The base language loads first and the current one overlays it**, so gaps fall back to
  English.
- Suffix conventions (`.Plural`, `.Short`) reserved.

**Culture is negotiated, never imposed, and two sets decide it.** *Supported* is every language
the catalogs can render — English is in it **by construction** (`LanguageOptions.Base` is `en`
and every base `strings.json` is written in it), so it is the settings picker's list and what a
forced choice is validated against, never a statement about what the shop serves. *Offered*
(`Language:Offered`, unset meaning `Language:Default` alone) is what this install serves somebody
who has chosen nothing.

One order, run **once, on the server**: the **forced choice** (the culture cookie, which may name
any Supported language) → the **browser's `Accept-Language`, among Offered** → **`Language:Default`**.
The client re-derives nothing — `Languages.Negotiate(preference, served)` prefers the saved row
and otherwise takes the language the server says it served, so a screen never flashes out of one
language into another between prerender and interactivity, and the browser is counted once
(baseservices.md).

The forced choice travels with the account, not with the machine: an **authenticated** read of
`GetLanguageSettings` leaves the device's culture cookie equal to the user's row — planted,
corrected, or deleted — and an **anonymous** one never touches it. Signing in on a device that
never carried the cookie, that session reads in the install's language and the next load reads in
the user's.
