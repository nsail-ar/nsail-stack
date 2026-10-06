# Settings

How a module contributes a settings item, how a POCO is stored, and the one scope that is
neither the company's nor a user's — what a machine remembers.

The design rule this exists for — **the default for a repetitive field lives in Settings** —
stays in [intentional-ui.md](../intentional-ui.md).

---

A contributed group in the main nav: each settings item is an ordinary page on its own
route — **no dynamic form engine, no settings shell**. Abstraction in `NSail.Settings`
(Stack); storage kit and the nav contribution in `src/Kits/Settings`.

- **Settings are typed POCOs serialized to JSON**. The type carries everything: storage key
  derived (`MetadataProvider.KeyFor(Type)`), `[SystemSettings]` marks system scope. **No
  merging**: a POCO is either **System** (one row, `UserId null`, readable without a user) or
  **User** (row per user; defaults are `new()`).
- **System scope when it is the company's fact; User scope when it is a way of working.** A
  *derivable* default (the active period by date) is the fallback when the setting is empty —
  the setting overrides the derivation, not the other way around. The control that means the
  concept reads the setting itself (`CurrencySelect`, `FiscalPeriodSelect`), so no screen
  repeats the lookup.
- **`SettingsManager`**: `Get<T>()`/`Save<T>()` — resolves key and scope from the type. The
  abstraction TryAdds a null implementation so apps without the kit still run.
- **Contribution**: each module implements `ISettingsContributor` returning nestable
  `SettingsItem`s (`Group`/`GroupIcon`/`Weight`) whose `PageType` names a concrete page.
  `SettingsMenu` — an `INavMenuContributor` — folds them into the nav's *Settings* group
  via `NavMenuItem.Merge(collapseSingleChildGroups: true)`. Each page is an ordinary
  `NsPage` gated by its own save message (`[Authorize<SaveThemeSettings>]`) and moving
  its POCO with its own `Get`/`Save` pair. Reference: `StorageSettingsContributor` +
  `StorageSettingsPage`.
- **Every User-scoped setting surfaces under Mi Cuenta → Preferencias** — never a standalone
  door of its own, and never the Settings group, which is the company's. The next per-user
  setting anyone builds is born there, and more personal items are expected to move under that
  menu over time. The mechanism is `PreferencesMenu`, an ordinary
  `INavMenuContributor` naming Iam's *MyAccount* group — not `ISettingsContributor`, which only
  ever feeds the Settings tree. Reference: `ThemeSettingsPage`/`LanguageSettingsPage`.
- **Reload, not hooks**: a page whose save changes culture or theme reloads explicitly
  in its own `OnSubmit` (`Reload()` / `NavigateTo(…, forceLoad: true)`) — there is no
  reload flag on the item. The saved language applies at boot; prerender follows the
  `.AspNetCore.Culture` cookie (`UseRequestLanguage` in the host), which
  `SaveLanguageSettings` writes on its own response beside the row — never the page by
  hand, so no second road to a chosen language can forget it. An authenticated
  `GetLanguageSettings` mirrors the row onto that cookie, so the choice reaches the
  account's other devices too ([localization.md](localization.md)).
- **A secret is write-only, and a derived `HasXxx` is how the screen still knows.**
  The read blanks the credential and answers a flag beside it; the save reads a blank field as
  *unchanged*, never as *clear it*, so editing any other field cannot silently take the install
  offline. The flag is `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]`: a
  plain `[JsonIgnore]` would keep it out of the stored row AND out of the answer, and this one
  attribute serves both ends — the save builds a fresh row that never sets it, so `false` is
  omitted from storage, and the read fills it, so `true` rides the wire. The screen draws it
  with `NsPasswordField IsStored` ([fields.md](fields.md)); no page mints a sentence of its own.
  **A property derived from the credential has to learn the flag too** — `WhatsAppSettings`
  `IsInForce` reads either arm, because the browser holds the flag over a blank field and every
  server-side reader holds the real credential off the stored row, where the flag is absent.
  The consequence, shared by the Meta/Google/Storage handlers: a stored secret can be replaced
  but not cleared through its own save.
  Reference: `SmtpSettingsHandler`, `WhatsAppSettingsHandler`, `SmtpSettingsWireShapeTests`.
- **A POCO the STACK reads is still an ordinary settings type** — `ThemeSettings` and
  `NavMenuArrangement` (`NSail.Settings`) both live beside the abstraction, and the kit's Sdk
  carries the messages that move them. `NavMenuArrangement` is System scope and holds what this
  install did to its own menu; what reads it is a Stack seam the kit fills
  (`INavMenuArrangement`), never a screen, and it has no settings page yet — both are
  [navigation.md](navigation.md)'s.

---

## Device memory

A third scope beside System and User: **per machine**. `DeviceMemory` (`NSail.Components`,
registered by `AddComponentServices`) — `Recall<T>(key)` / `Remember<T>(key, value)` over the
browser's own `localStorage`. Whoever signs in at that counter gets the counter's answer.

- **Not a setting, and the distinction is the whole point.** A setting is deliberate
  configuration a user states once; device memory is a habit the machine picks up from being
  used. A screen that would ask "which one?" and always get the same answer *at this terminal*
  wants this; a company fact or a way of working wants a settings POCO.
- **Keys are the caller's**, explicit and never derived from the control: the point of sale
  rides one key (`"point-of-sale"`, held by `PointOfSaleSelect`) so every screen that asks
  offers the same drawer. A per-screen filter picks a per-screen key.
- **Every read failure answers the default, silently** — never written, no longer parses,
  a browser that refuses to store. The caller's fallback is then exactly what it would have
  done with no memory at all, so a remembered value can only ever improve a prefill.
  Nothing secret rides here: what is stored is authorized by the server on every read.
- **Recall on an interactive render, never in `OnInitializedAsync`**: JS interop cannot run
  while the page prerenders (both apps are `InteractiveWebAssembly`, which is also why
  `ProtectedLocalStorage` — Server-container only — is not the mechanism). And not the first
  render either when the control loads its own options: that render can land before the
  lookup answers. Reference: `PointOfSaleSelect.OnAfterRenderAsync`.
- **The write is the user's pick, not the form's save and not the prefill.** A machine that
  has not chosen keeps falling back; it must not adopt a default it was never told to use.
- Riders named and deliberately unbuilt: list filters, form drafts, `StoreSelect`.

