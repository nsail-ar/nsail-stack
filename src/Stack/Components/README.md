# Components

The Blazor UI library. Components expose what the user means to do and what kind of data is
shown (`NsMoneyField`, `NsSubmit`, `As="NsAs.Main"`), never a vendor's variants, so a screen
reads like application meaning and the renderer underneath can change. Labels, titles and
columns are derived from the message or model a screen binds, not typed.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Components` | Vendor-free semantics: `NsPage`/`NsPartial` base classes, `NsForm`, field bases, routing and surfaces, navigation menu, actions, dashboard/guide/onboarding/skills contributors, brand and theme types, `NsIcons` catalog types, `[Authorize<TMessage>]` | Web, client (through `Components.Mud`) |
| `NSail.Components.Mud` | The renderer: every `Ns*` component drawn with MudBlazor, the shell (`NsMainLayout`, `NsNavMenu`, `NsSurface`), `NsIcons`, `ns-mud.css`, the Stack's own `strings.json`/`strings.es.json`, `AddComponentServices()` | Blazor client; the Web host through `NSail.BaseServices.WebApp` |
| `NSail.Components.DaisyUI` | Scaffold for a second renderer; carries no components yet | nobody |

MudBlazor stays inside `Components.Mud`: no public NSail API takes a `Variant`, `Color` or `Mud*`
type, and a page never names one. Kit and app screens are plain Razor composing `Ns*`.

## Use it

The client project (`Microsoft.NET.Sdk.BlazorWebAssembly`) references `NSail.Components.Mud`,
embeds its `strings*.json` ([Localization](../Localization/README.md)) and registers:

```csharp
builder.Services.AddComponentServices();   // Mud services, route table, dialogs, settings, strings

services.AddRoutesFromAssembly();          // pages of this assembly
services.AddStringsFromAssembly();
services.AddNavMenu<Menu>();               // INavMenuContributor
services.AddMessagePolicies();             // so [Authorize<TMessage>] can be answered
```

The host document is `NsApp` (from `NSail.BaseServices.WebApp`) with `NsAssets`, `NsSplash`,
`NsSetup` and the client's `Routes`; `Routes.razor` is `<NsRouter DefaultLayout="...">` with a
`NsPageNotFound`; the layout composes `NsMainLayout` (`Header`, `Drawer`, `Content`, `Aside`,
`Companion` slots) with `NsNavMenu`, `NsContainer` and `NsSurface` regions. The Sample's
`App.razor`, `Routes.razor` and `Layouts/MainLayout.razor` are the shape to copy.

A page inherits `NsPage` (deny-by-default: it requires a signed-in user unless `[AllowAnonymous]`),
is guarded by the message it sends, and binds messages:

```razor
@page "/contacts/new"
@attribute [Authorize<CreateContact>]
@inherits NsPage

<NsForm TModel="CreateContact" Model="_model" OnSubmit="OnSubmit">
    <NsPanel>
        <Header><NsTitleBar Title="@GetTitle()" /></Header>
        <Content>
            <NsFormGrid>
                <NsFormGridItem Grow><NsTextField @bind-Value="_model.Name" AutoFocus /></NsFormGridItem>
            </NsFormGrid>
        </Content>
        <Footer><NsSubmit /></Footer>
    </NsPanel>
</NsForm>
```

## Configuration

**Registration** (`NSail.Components`, all on `IServiceCollection`):

| Call | Registers |
|---|---|
| `AddComponentServices()` (Mud) | MudBlazor services (snackbar bottom-center), `AddRouteTable()`, `DialogManager`, `ProblemManager`, `DeviceMemory`, `IDeviceProvider`, `PageGate`, `NavMenu`, `Companion`, this assembly's strings, `AddSettings()`, a default `IBrandProvider` and `IThemeProvider` (`TryAdd`) |
| `AddRoutesFromAssembly(assembly?)` | An `IRouteContributor`; typed URLs come from `GetUrl<TPage>` |
| `AddMessagePolicies()` | `[Authorize<TMessage>]` support; both hosts call it |
| `AddNavMenu<T>()`, `AddNavMenuCount<T>()`, `AddNavMenuSearch<T>()`, `AddNavMenuArrangement<T>()` | Menu entries, counts behind an entry, drawer search results, an install's order/label/visibility |
| `AddActions<T>()` | `IActionContributor<TOutlet>` for every outlet it implements |
| `AddDashboard<T>()` | `IDashboardContributor` / `IDashboardContributor<TSubject>` |
| `AddOnboarding<T>()` | `IOnboardingContributor` (wizard steps) |
| `AddGuide<T>()`, `AddGuidePage<TPage>()` | Guide chapters and the page that shows them |
| `AddSkills<T>()` | `ISkillContributor` playbooks |
| `AddRouteGate<T>()` | `IRouteGate`, a router-level veto asked per route |

**Brand and theme.** `IBrandProvider.GetBrand()` answers the active `Brand` (`Name`, `Typeface` key
from `Typefaces`, `DefaultDark`, `Light` and `Dark` `BrandTheme`), or `null` while not yet known.
Without one, `StaticBrandProvider(new Brand())` applies; a per-tenant provider replaces it:

```csharp
services.AddSingleton<IBrandProvider>(new StaticBrandProvider(new Brand { Name = "Acme Tasks" }));
```

A `BrandTheme` stores only what is picked (`Accent`, `Chrome`, logo, status colours); surfaces and
inks are derived from it. `IThemeProvider` answers per-user `ThemeSettings` (`DarkMode`, null
follows the OS).

**Icons.** `NsIcons` is the generic vocabulary (`Home`, `Add`, `Edit`, `Delete`, `Save`, ...), each a
`Glyph` of inline SVG. An app declares its own catalog of `Glyph`s for domain icons; the plugin's
`new-icon` skill adds one.

**Semantic enums**, statically imported in `_Imports.razor`: `NsAs` (`Default`, `Main`, `Important`,
`Inline`, `Submit`, `Filter`, `Send`, `Danger`), `NsTextAs`, `NsSize`, `NsAlign`, `Surfaces`.
`NsBreakpoint` is written in full (`Breakpoint="NsBreakpoint.Never"`).

**Component families** (the contract of each is in its doctrine page):

| Family | Components | Contract |
|---|---|---|
| Pages and base | `NsPage`, `NsPartial` (`Send`, `Publish`, `Subscribe`, `Translate`, `Confirm`, `Notify`, `GetTitle`, `GetAction`, `GetLink`, `GetUrl<TPage>`), `Runner` | [surfaces.md](../../../plugins/nsail-stack/doctrine/ui/surfaces.md) |
| Forms | `NsForm` (`Model`, `OnLoad`, `OnSubmit`, `OnReload`, `Untracked`, `KeepOpen`, `Disabled`, `ReadOnly`), `NsSubmit`, `NsClose`, `NsFieldRefusal`, `NsRecordState`, `NsSendMessage` | [forms.md](../../../plugins/nsail-stack/doctrine/ui/forms.md) |
| Fields | `NsTextField`, `NsEmailField`, `NsPhoneField`, `NsUrlField`, `NsTextArea`, `NsPasswordField`, `NsSearchField`, `NsNumericField`, `NsMoneyField`, `NsPercentField`, `NsDateField`, `NsTimeField`, `NsDateTimeField`, `NsDurationField`, `NsColorField`, `NsCheckBox`, `NsSelect`, `NsMultiSelect`, `NsAutocomplete`, `NsRadioGroup`, `NsFileUpload`, `NsMultiFileUpload`, `NsHelp`, `NsHint`, `NsMissing`. Common parameters: `Label` (derived when omitted), `Value`, `For`, `Disabled`, `ReadOnly`, `Placeholder`, `Helper`, `Required`, `AutoFocus`, `Grow` | [fields.md](../../../plugins/nsail-stack/doctrine/ui/fields.md) |
| Actions | `NsButton`, `NsAction`, `NsLink`, `NsPageLink`, `NsActionOutlet`, `NsActionToolbar`, `ActionItem`; navigating is `NsLink`, executing is an action component | [actions.md](../../../plugins/nsail-stack/doctrine/ui/actions.md), [menus.md](../../../plugins/nsail-stack/doctrine/ui/menus.md) |
| Hosts of many things | `NsTable`/`NsTh`/`NsTd` (`Rows`, `OnQuery`, `RowTemplate`, `RowEditor`, `Mode`, `Exportable`), `NsTabs`/`NsTab`, `NsListEditor`, `NsTree`, `NsTimeGrid`/`NsTimeBlock`, `NsDashboard`/`NsSubjectDashboard`, `NsLoad` (`OnLoad`: loading, content, failure with retry) | [hosts.md](../../../plugins/nsail-stack/doctrine/ui/hosts.md) |
| Layout and styling | `NsPanel`, `NsPageHeader`, `NsTitleBar`, `NsToolBar`, `NsFormGrid`/`NsFormGridItem`, `NsStack`, `NsCard`, `NsPaper`, `NsExpander`, `NsAlert`, `NsText`, `NsChip`, `NsStatusText`/`NsStatusDot`, `NsMeter`, `NsTooltip`, `NsSince`, `NsRemoteImage`, `NsIcon` | [styling.md](../../../plugins/nsail-stack/doctrine/ui/styling.md) |
| Shell and surfaces | `NsRouter`, `NsMainLayout`, `NsContainer`, `NsSurface` (`Surfaces.Aside`, `Modal`, `Dialog`), `NsDrawer`, `NsDialog`, `NsCompanion`, `NsErrorBoundary`, `NsPageNotFound`, `NsPageError`, `DialogManager` | [surfaces.md](../../../plugins/nsail-stack/doctrine/ui/surfaces.md), [focus.md](../../../plugins/nsail-stack/doctrine/ui/focus.md) |
| Navigation | `NavMenuItem` (`Name`, `Icon`, `PageType`, `Weight`, `Permission`, ...), `INavMenuContributor`, `NsNavMenu` | [navigation.md](../../../plugins/nsail-stack/doctrine/ui/navigation.md) |
| Guided flows | `NsWizard`, `WizardContext`, `NsFullLayout`, `IRouteGate`; `NsGuide`, `NsGuideLink`; `ISkillContributor` | [wizard.md](../../../plugins/nsail-stack/doctrine/ui/wizard.md), [guide.md](../../../plugins/nsail-stack/doctrine/ui/guide.md), [skills.md](../../../plugins/nsail-stack/doctrine/ui/skills.md) |
| Branding and settings | `Brand`, `BrandTheme`, `IBrandProvider`, `IThemeProvider`, `LanguageSettings`, `DeviceMemory` | [branding.md](../../../plugins/nsail-stack/doctrine/ui/branding.md), [settings.md](../../../plugins/nsail-stack/doctrine/ui/settings.md) |

**Labels and keys.** Fields read their label from their binding expression, `NsTh`/`NsTd` from
`For="() => context.Name"`, a page title from its type (`GetTitle()`); a key with no text renders
as itself. See [Localization](../Localization/README.md) and
[localization.md](../../../plugins/nsail-stack/doctrine/ui/localization.md).

## How it fits

- Sends and publishes through [Messaging](../Messaging/README.md) (`Send`, `Publish`, `Subscribe`);
  a page is guarded by the message it sends ([Security](../Security/README.md)).
- Keys come from [Localization](../Localization/README.md) and [Metadata](../Metadata/README.md).
- Per-user and per-device preferences (language, theme, columns) go through
  [Settings](../Settings/README.md); list export uses [Exporting](../Exporting/README.md).
- Refusals arrive as `Problem`s from [Types](../Types/README.md) and are placed on the field they name.
- The host half of the shell (`NsApp`, request localization, the request-side `IDeviceProvider`) is
  [BaseServices](../BaseServices/README.md). [React](../React/README.md) is the other client, with
  the same keys.

## Going deeper

- Guide: [getting-started.md](../../../docs/getting-started.md), section "The Blazor client"
- Doctrine: [intentional-ui.md](../../../plugins/nsail-stack/doctrine/intentional-ui.md) (the rules a screen is built by), [ui/README.md](../../../plugins/nsail-stack/doctrine/ui/README.md) (the per-component shelf)
- Sample: [NSail.Sample.Wasm](../Sample/NSail.Sample.Wasm) (`Contacts/` pages, `Layouts/MainLayout.razor`, `Screens.cs`), host document in [NSail.Sample.Web/App.razor](../Sample/NSail.Sample.Web/App.razor)
- Tests: [test/Stack/Components](../../../test/Stack/Components) (bUnit)
