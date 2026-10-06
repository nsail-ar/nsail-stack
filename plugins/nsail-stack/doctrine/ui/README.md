# The UI reference shelf

The per-component contracts and the Stack-internal seams. **The rules a screen is designed by
are not here** — they are [intentional-ui.md](../intentional-ui.md), and the record behind them
is [intentional-ui-cases.md](../intentional-ui-cases.md). Read the page for the thing you are
placing.

| Page | Covers |
|---|---|
| [fields.md](fields.md) | `NsTextField`, `NsSearchField`, `NsPasswordField`, `NsSelect`, `NsMultiSelect`, `NsAutocomplete`, `NsRadioGroup`, `NsDateField`, `NsDateTimeField`, `NsNumericField`, `NsMoneyField`, `NsColorField`, `NsFileUpload`, `NsMultiFileUpload`, `NsField`, `NsFieldRefusal`, `NsHelp`, `NsMissing` |
| [actions.md](actions.md) | `NsButton`, `NsLink`, `NsAction`, `NsPageLink`, `NsSubmit`, `NsClose`, `NsActionOutlet`, `NsActionToolbar`, `ActionItem`, the contribution mechanism, a grid row's action cell |
| [forms.md](forms.md) | `NsForm`: the read half (`OnLoad`, `Document`, the three states), tracking, the exit guard, closing, `ApplyProblem` |
| [hosts.md](hosts.md) | `NsTable`/`NsTh`/`NsTd`, `NsTabs`/`NsTab`, `NsListEditor`, `NsTimeGrid`/`NsTimeBlock`, `NsDashboard`/`NsSubjectDashboard`, `NsLoad` |
| [styling.md](styling.md) | `NsFormGrid`/`NsFormGridItem`, `NsStack`, `NsCard`, `NsPanel`, `NsPaper`, `NsExpander`, `NsText`, `NsRemoteImage`, `NsTooltip`, `NsMeter`, `NsStatusText`/`NsStatusDot`, `ns-mud.css`, container queries |
| [surfaces.md](surfaces.md) | The base chain, and the query, size, stacking and announce seams |
| [menus.md](menus.md) | `NsMenu`, `NsMenuItem`, `NsMenuBlock`, `NsMenuDivider`, `ActionItem.Menu` |
| [navigation.md](navigation.md) | `NavMenuItem`, `INavMenuContributor`, `INavMenuCount`, `NavMenuCountsChanged`, `NsNavMenu` |
| [wizard.md](wizard.md) | `NsWizard`, `WizardContext`, `IRouteGate`, `NsFullLayout` |
| [settings.md](settings.md) | `SettingsManager`, `ISettingsContributor`, `SettingsItem`/`SettingsMenu`, `DeviceMemory` |
| [guide.md](guide.md) | `IGuideContributor`, `GuideItem`, `GuideReader`, `GuideRoute`, `NsGuide`, `NsGuideLink`, `Markdown.ToHtml` as a chapter's renderer |
| [branding.md](branding.md) | `Brand`, `BrandTheme`, `IBrandProvider` |
| [localization.md](localization.md) | `StringManager`, `IStringSource`, the derived keys, culture negotiation |

Not on the shelf, because nothing but the layout composes them: `NsDialog`, `NsOpenDialog`,
`NsDialogExit` (the dialog's own, and its rule is [actions.md](actions.md)'s), `NsMainLayout`,
`NsContainer`, `NsDrawer`, `NsSurface`, `NsApp`, `NsRouter`.
