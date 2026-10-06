# The guide

How a module contributes its own chapter of the user guide, what an app's guide screen does
with them, and how a screen puts a door to it in its own header.

The family is named Guide throughout (`IGuideContributor`, `NsGuide`, never `Manual*`);
"Manual" survives only as the Spanish word the menu reads. The guide is split per kit, so every
app composing a kit reads that kit's guide — the same shape settings use:
[settings.md](settings.md) is the twin to read beside this.

---

Abstraction in `NSail.Components` (Stack), beside the contributor seams it copies —
`INavMenuContributor`, `IDashboardContributor`, `IOnboardingContributor` — and beside
`Markdown.ToHtml`, which is what draws a chapter. No storage, no manager, no kit of its own:
a guide is content a module already knows, not state anybody saves.

- **Contribution**: a module implements `IGuideContributor` (`Task<IReadOnlyList<GuideItem>>
  GetItems()`) and registers it with `services.AddGuide<Guide>()` in its `Add<Kit>Components`
  setup. The class is named by the contribution alone — `Guide`, at the Shared project root
  beside `Menu.cs` (structure.md).
- **The prose is never in an assembly.** A chapter is a markdown file under the contributing
  project's `wwwroot/guide/{culture}/`, images beside it, served as an ordinary Razor
  class-library static web asset at `_content/{Assembly}/guide/{culture}/{file}.md`. A kit
  ships text the way it ships a stylesheet; nothing is embedded, nothing is compiled, and the
  file is editable without a rebuild of anything that reads it.
- **`GuideItem`** carries `Name` (the localization key of the chapter's title, resolved as
  `Guide.{Name}` — the same role `Name` plays on `NavMenuItem` and `OnboardingStep`), `File`
  (the markdown's own name, with no culture and no extension) and `Weight` (`NavMenuItem`'s
  bands: the trade's own material 10–50, cross-cutting machinery 80+). **`File` is also the
  chapter's address** (`/guide/{file}`), so nothing writes a second name for the same thing.
  `Assembly` is stamped by `GuideItem.Collect` from the contributor's own type — the class
  that contributes a chapter and the project that ships it are one project by construction, so
  no contributor ever writes an assembly name. `Collect` orders every contributor's chapters by
  weight, ties keeping contribution order.
- **The body is fetched when the chapter opens** (`GuideReader`, registered by `AddGuide`):
  the UI culture's file first, then `GuideItem.DefaultCulture` (`es`) — a guide nobody
  translated is read in one language. Every chapter read stays read for the scope, so paging
  back and forth costs one request per chapter. **The read happens after the first render, never
  in `OnInitialized`**: a server prerender that fetched it would be the host asking itself for
  its own static file.
- **Registration is the whole composition, and nothing filters afterwards.** An app that never
  composed a kit never runs that kit's `AddGuide`, so the kit's chapters cannot reach that
  app's guide — Therapy keeps books and sells nothing, so it reads Accounting's chapter and
  no counter chapter exists for it to hide.
- **`NsGuide`** is to `IGuideContributor` what `NsNavMenu` is to `INavMenuContributor`. It
  takes the open chapter's `File` and draws two things: the **index** and the chapter's body
  through `Markdown.ToHtml` inside `.ns-markdown` (`ns-mud.css`).
- **The screen is the app's**, an ordinary `NsPage` over `NsPanel` + `<NsGuide Chapter="..." />`,
  routed at both `/guide` and `/guide/{Chapter}`, with its own nav entry. The app registers it
  with `services.AddGuidePage<GuidePage>()` so every door to the guide resolves an address
  through the route table instead of writing one. An app whose material is not open to every
  session gates the screen like any other (`[Authorize<T>]`, permissions.md) — the guide page
  carries no policy of its own. Reference: `NSail.Optical.Shared/Guide/GuidePage.razor`.
- **The screen declares `Xl` only on the main surface**: `GuidePage.OnCreated` calls
  `SetSize(Xl)` when `Surface is null || Surface.IsMain`, so the manual takes the window as main
  content and keeps the aside's own Md measure when a guide button opens it.

## What a chapter is

**One chapter is one story a kit has to tell, a few screens long** — never the kit's whole
surface in one file. A kit with more than one story ships more than one chapter: a second one
costs a `GuideItem` and a file, and buys the reader an address that is about a single thing.
Weight orders them, so the two land beside each other in the index anyway.

- **A chapter that grew past a few screens is split along the line its own `##` sections already
  draw**, and each half's opening paragraph links the other — Accounting's money and Accounting's
  paper, Scheduling's turnos and Scheduling's day. **Length alone is not the test**: one screen's
  whole circuit is one story however many sections it takes.
- **A section lives with the kit that owns the screen it walks**, not with whichever chapter was
  written first, and a story spanning two kits is split at the kit line with each half linking
  the other.
- **A happy path's paragraph rides with the feature, and its `##` title is the same words as the
  E2E's name** (testing.md, ruling 13): every expected outcome a screen ships is a section here
  and a test there, under one name, so the two are found together.
- **Splitting a chapter moves its addresses**: every `Guide="..."` door, every `/guide/{chapter}`
  link in another chapter and each app's own chapter table follow the section that moved.
  `GuideTopicTests` catches the doors; nothing catches the prose links but a reader.
- **The chapter's title is localized; its prose is not.** A chapter's title goes through
  `StringManager` like any other chrome; the file's text stays in the voice and language its
  owner wrote it in (es-AR today), because a guide nobody translated is better read in one
  language than rendered as its own keys.

## The markdown

**The grammar is `Markdown.ToHtml`'s with `MarkdownOptions.Document`** — headings 1–6,
blockquote, ordered and unordered lists, paragraphs, bold and italic, a link, **an image**
(`![alt](file.png)`, resolved against the chapter's own folder) and **a pipe table** (a header
row over a `|---|` rule). There is no lazy continuation: a list item is one physical line, and a
second line under a bullet ends the list and starts a paragraph — `GuideChapterFileTests`
enforces this over every chapter `GuideShelf` knows, in every language it ships.
Everything else passes through as literal text, HTML-encoded. `MarkdownOptions.Prose` — the
default, and what every channel on the send path renders with — admits neither image nor table,
so the same source read by a WhatsApp body is exactly the text its author typed.

**A chapter names another chapter by its own address** — `[Cobertura](/guide/coverage)`,
`[Comprobantes](/guide/vouchers#comprobantes)` — and `Document` renders an address rooted at the
app as a link for exactly that reason (`MarkdownOptions.AppLinks`). `Prose` does not: text
leaving over a wire has no host to resolve a `/` against, so there the address stays the literal
characters its author typed. A protocol-relative `//host` is nobody's app and stays literal in
both.

**A chapter names one of its own sections the same way, minus the chapter** —
`[QR](#el-qr-del-comprobante)` — and `AppLinks` renders a bare fragment as a link for the same
reason: the section's heading already carries that id in the document being drawn. `Prose` stays
literal here too — a WhatsApp body has no document at all to land a fragment in, not merely no
host to resolve one against.

## The index

**One line per entry, the shape `NsWizard`'s progress rail already has** ([wizard.md](wizard.md)),
and it is the table of contents and the navigation at once.

- Every chapter, in weight order, and under the open one **its own sections** — the level-2
  headings the chapter's markdown carries, read off the file when it arrives. The chapter list
  comes from the contributors and costs no fetch; the sections appear with the chapter.
- **The open chapter and the section the address names are marked** (`.ns-guide-entry-current`) —
  a wash and the brand's hue as INK, and the rail restates both in the band's own vocabulary
  inside the drawer, where the ground is the chrome and not a card
  ([branding.md](branding.md), The accent as ink).
- **On a phone the rail collapses into the drawer, exactly as the nav does.** The onboarding
  reaches the drawer through `NsFullLayout`'s `Menu` slot because it owns its whole frame; a
  routed screen does not, so **the page announces its index to its surface and the surface's
  chrome draws it** — `SurfaceContext.AnnounceIndex`, the channel `Announce` already is for a
  title and its utilities. On the main surface that chrome is the drawer `NsNavMenu` fills, and
  the index goes **above** the app's map, never instead of it: the frame has one gutter and a
  page borrows it rather than taking it.
- **The two places the rail can stand trade on the drawer's own breakpoint**, both halves written
  in the markup with `NsResponsive` — `ShownFromWindow(Md)` on the column beside the content,
  `ShownBelowWindow(Md)` on the drawer's copy — so one of them is always on screen and never
  both. The **window**, not the container: this rail trades places with a drawer that docks or
  hides on exactly that edge, which is the one exception [styling.md](styling.md) names.
- **Inside an overlay the index is a chapter switcher instead** (an `NsMenu`, which is what a
  short list of destinations is) with the open chapter's sections under it. That is not a
  narrower rail, it is a different answer to a different question: the aside a guide button
  opens has the frame's drawer *behind* it and out of reach, and no width for a second column.
  **The hosting surface decides which face is drawn** (`SurfaceContext.IsMain`), never the
  caller, so one screen is written once and reads correctly through both doors.

## Anchors

**Chapter = route segment, section = the heading's slug.** Every heading `Markdown.ToHtml`
draws carries `id="{slug}"` — lowercase, accents stripped, everything else a hyphen — and
`Markdown.Headings` reads the same slug off the same text, so the index and the anchors it
points at are one derivation. `/guide/{chapter}#{slug}` scrolls to that section on arrival and
again whenever the fragment changes (`nsapp.scrollToId`).

**A fragment rides the resolved href, not the route** — `NsLink Fragment`
([actions.md](actions.md)).

**A section is a place, so Back returns to it.** On the guide's own screen a section link takes
a history entry of its own — the fourth transition `SurfaceContext.Follow` names
([surfaces.md](surfaces.md), Opening and matching), a same-path move that changes only the
fragment — so Back leaves the reader on the section they were
reading before it instead of out of the manual. **Inside an overlay it replaces instead**: the
guide's aside is one place and its X spends one entry (Targets rule 4,
[../intentional-ui.md](../intentional-ui.md)), so the reader still closes it in one gesture
however many sections they read. The scroll is spent once per fragment and forgotten when the
fragment goes away, which is what lets the same section be clicked again after a Back.

## `NsGuideLink`

**The door to the guide is a button, and it is not `NsHelp`.** `NsHelp` stays a field's
two-line whisper in a popover — never a chapter, never a link.

- `Topic` is `{chapter}` or `{chapter}#{section}`. `Label` is "Ayuda" unless the screen says
  otherwise — "Instrucciones" where the screen is a sequence of steps.
- **The glyph alone by default** (`NsIcons.Help`, the same circled question a field's own help
  wears — one glyph for both), its word the tooltip and the accessible name. A screen that wants
  the word sets `Breakpoint`, which then reads exactly as it does on every other control: the
  width from which the label shows. **The rank never moves with it** (icon-only is a state of
  the label, not a rank): a door to the guide is a title bar's utility, so it is `Inline` at
  every width, the same rung `NsHelp` wears.
- **It opens `Surfaces.Auto`** — beside the screen on a desktop, full screen on a phone — so a
  half-filled form is still there when the reader is done, and **no browser tab is ever
  opened**.
- **An app that registered no guide page draws no door at all**: a link to a room nobody built
  is worse than no link. **Nor does a session that may not open the page it registered** — the
  link asks the same `PageGate` the drawer asks, on the guide page's own type, so a door a click
  would only turn into a refusal is never drawn either.

**`NsPageHeader Guide` is how a screen adds it**, and the header places it itself — last in the
title row, past whatever utilities the page announced — so every screen's way into the guide is
in the same corner and no page positions it.
