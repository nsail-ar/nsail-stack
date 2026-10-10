# Fields, lookups and files

The contracts of the input components. Read the part for the control you are placing.

The rules that decide *which* control and *what* it may say — intent over vendor, the combo
that comes full, the helper that is one line — stay in
[intentional-ui.md](../intentional-ui.md). The record behind them is in
[intentional-ui-cases-controls.md](../intentional-ui-cases-controls.md), under the same section names; a rule
marked *(cases)* has its story there.

---

## The catalog

`NsTextField`, `NsSearchField`, `NsPasswordField`, `NsSelect`, `NsMultiSelect`,
`NsAutocomplete`, `NsRadioGroup`, `NsDateField`, `NsDateTimeField`, `NsMoneyField`,
`NsColorField`, `NsFileUpload`, `NsMultiFileUpload`, plus `NsField` — field chrome alone
(label, outline, helper, problem line), for a field operated through composed controls
rather than one input. `NsFileUpload` and `NsRadioGroup` are built on it. They inherit
`NsFieldBase`, `NsAsyncFieldBase` or `NsLookupBase` (`NSail.Components`).

- **`NsFieldRefusal` is the catalog's one member with no chrome AT ALL** — no input, no label, no
  outline, only the refusal line — and it exists because **being rendered is what anchors an
  issue**: a member a composed editor edits without rendering a field for it (an attendee list
  whose rows are `PartyRef` while the refused member is a `List<Guid>`, so the list cannot share
  its `TValue`) has nothing to announce it, and its refusal falls to the form's foot as the
  generic "Valor inválido". The editor places it; the **PAGE** writes its `For` against the model
  member, because `FieldIdentifier` resolves the object the member hangs off and an expression
  written inside the editor would name the editor. Clean it renders nothing, so the member costs
  the surface no height until something refuses it, and the words it does draw announce
  themselves (`role="alert"`): with no input there is no `aria-describedby` to hang on anything,
  so the clipped twin below is withheld too. Where the member IS rendered as an input, every
  other row of this catalog is the answer and a screen writes nothing
  ([forms.md](forms.md), *Placing a refusal*).

- **`NsRadioGroup` is for a closed, unchanging option set small enough to show whole** — the
  Items/ItemValue/ItemText projection is the same one `NsSelect` reads (an `IOption` sequence
  from `GetItems<TEnum>`, or a caller's own projection), so a screen swaps one for the other
  without touching how the list is built. `MudRadioGroup` draws no floating label of its own,
  so it composes `NsField` for that chrome rather than the raw vendor input `NsSelect` wraps.
- **Masks**: `NsTextField` takes a `Mask` in NSail's own vocabulary (`0` digit, `a` letter,
  `*` either, everything else a literal), presentation only — the bound value stays raw. The
  patterns are the consuming kit's knowledge (`PartyMasks`). **A date field wears none**, and
  that is a rule rather than an omission: see the typing contract below.
- **`NsHelp` takes one parameter, `Key`** — a **global** localization key, because the
  component cannot see the page whose prefix would scope it.

### Dates

- **`NsDateField` hints its own shape**: the placeholder is the culture's field order in the
  language's letters, derived from both and hardcoded in neither.
- **A date box belongs to the browser until the field is left.** A mask on a MudPicker drops the
  vendor's own `onchange` wiring and drives **every keystroke through `oninput` into .NET**, so
  each key is a round trip that rewrites the whole box and a key typed while one is in flight is
  overwritten by the reply — a typed date that silently does not stick, under load, at any pace,
  Server render and WebAssembly alike. Unmasked, nothing is bound to the input event at all and
  the whole text arrives once, on change. What a mask would buy is delivered at **parse** time
  instead, by `DateTextConverter`, the one seam both date fields read and write through:
  - The separators need not be typed: the shape already says where they fall, so `07082026` is
    the same day as `07/08/2026`, and so are `7/8/2026` and `07/08/26`. The **field order is the
    culture's and is never negotiable** — the same digits are a different day under `es-AR` than
    under `en-US` — but the separators are, since the order is what carries the meaning.
  - Text the field cannot read raises **its own refusal** (`Problems.UnreadableDate`, naming
    the shape) and is **left standing in the box**, so the person can see what to fix. The
    vendor's alternative is its own English sentence over a box wiped blank.
  - **A refusal writes nothing and blocks the submit.** The vendor raises the same
    `DateChanged(null)` for a box somebody emptied and a box holding words, and only the
    converter can tell them apart, so the field writes the value through **only when nothing was
    refused** — a non-nullable binding would otherwise take `default(DateOnly)` (0001-01-01) or,
    on `NsDateTimeField`, today at the hour already chosen: a date nobody entered, under a
    visible refusal saying no date was read. The model keeping what it held is only half the
    answer, because Guardar would then save that other date as though it were the one on
    screen — so the refusal is also posted to the `EditContext` through `NsFieldBase`'s
    `GetOwnProblem`, the seam `Required` already travels, and `Validate()` answers false for as
    long as the box holds text nobody can read. **`GetOwnProblem` is the seam for any field
    whose own input can be unreadable**, not a date one: override it, call
    `NotifyOwnProblemChanged()` when the answer changes, and never hand a page an unreadable
    input as a value. A consequence worth knowing before writing an e2e: a form whose ONLY edit
    is an unreadable date is never dirty, so `NsSubmit` never lights up at all — reproducing the
    blocked save in a browser means landing a real edit first (`WorkOrderPromiseMovedTests`).
  - The other fields on that seam are `MailCopyAddressesField` (Directory) and `NsUrlField` —
    for the latter, the form is `novalidate`, so the native gate a `type="url"` input used to
    carry is the field's own rule now, read off the field's KIND rather than off a declaration
    because a URL box is a URL box wherever it stands. **Three rules come with owning a kind's
    gate.**
    - **A rule standing in for the browser's is MATCHED to it, never tightened**: what the
      browser took is already saved in some install, and a stricter box holds every Guardar on
      that screen over a value nobody came to edit. `Uri.TryCreate(..., Absolute)` plus the text
      carrying the scheme it parsed as is that reading — `IsWellFormedUriString` is not (it
      wants the escaped canonical form and refuses a space, a brace, a backslash Chromium
      normalizes), and `TryCreate` alone is loose the other way (it reads `//acme.com/x` as a
      path under a `file` scheme the text never carried, which Chromium refuses). **Where a
      match is impossible, the gap is ONE named class and its cost is written down** — here, a
      domain no parser can read: Chromium takes `https://acme example/x`,
      `http://ac me.com`, `https:/acme.com` and `C:\x\y` as valid, `Uri` refuses all four
      (measured, Chrome 154 and Playwright's own build). Kept refused, because the house's one
      URL box is a link that travels literally into a message somebody receives — so the cost
      stands for that class alone: an install holding such an address opens the screen clean but
      cannot save it until the link is fixed.
    - **A refusal names its own motive, not the kind's.** `Problems.UnreadableAddress` says to
      write the address whole, which is a lie over `https://acme example/x` — that one is whole.
      The second motive gets the second row (`Problems.UnreadableAddressDomain`): a field owning
      a gate owes as many sentences as the gate has reasons to refuse.
    - **A refusal read off `Value` rather than off a keystroke posts through `GetOwnProblem`
      ONLY**, never through a `GetErrorText` of the field's own: the date boxes and
      `MailCopyAddressesField` can draw theirs straight because it is empty until somebody
      types, while one computed from the value is true on the render the form opens on — drawn
      straight it greets the person refused over what the model brought, and refuses every
      letter of a scheme being typed. `NsFieldBase` already holds the moment for both halves.
  - The converter must reach the picker **before** the value does — Blazor assigns parameters
    in source order and the vendor writes its box as `Date` lands — and its `TextChanged` is
    observed **one way out only**, to redraw and to re-post the refusal. Never write `Text` back
    in: that is the clobber.
- **`NsDateTimeField` is two boxes and ONE field, so a refusal marks both of them and says its
  sentence once.** A red date box beside a grey time box reads as the hour being fine when what
  was refused is the instant the pair spells together. The time half takes the vendor's own error
  class and an `aria-invalid` splat rather than the vendor's `Error` parameter: `Error` also
  renders the helper container under that box, with an empty line in it (measured,
  `NsDateRefusalSubmitTests`), and the words belong to the date box above — one field, one
  message, however many boxes it wears.
- **`NsDateField` takes no bound and derives one.** A field whose bound member declares
  `[NotFuture]` (`NSail.Messaging.Runtime.Validation`) hands the vendor a ceiling of the house's
  own `BusinessDate.Today`, so the calendar never offers the day the submit will refuse — the
  required mark's shape, one declaration read by both ends. A member declaring nothing keeps the
  vendor's unbounded calendar, and a value **already past the bound still displays**:
  MudDatePicker reads the ceiling when a day is picked or typed, never when one is handed in
  (measured, `NsCodedRefusalTests`), so deriving it cannot quietly empty a row on file. There is
  no `Min`/`Max` parameter here on purpose — the difference from `NsMoneyField` below is whose
  rule it is: money's bound is the **caller's** (a range no annotation can express, said on the
  field), a date's is the **message's**, and a page that wrote it beside the declaration would be
  keeping a second copy of it.

### Numbers and money

- **Every numeric box reads and writes the READER's notation.** The vendor's `Culture` defaults
  to `CultureInfo.InvariantCulture`, under which a Spanish keyboard's decimal comma is a
  THOUSANDS separator: "0,5" arrives as 5 and "−0,25" as −25. `NsNumericField`,
  `NsPercentField`, `NsDurationField` and `NsMoneyField` all pass `RegionCulture.For(Language)`
  — the app's language mapped to a region once, never a `CultureInfo` per field and never the
  server thread's. A new field over a vendor input that parses text owes the same parameter;
  the box is plain `type="text"` with `inputmode="decimal"`, so no browser locale stands between
  what is typed and what the converter reads (measured, `NsNumericFieldCultureTests`).
- **…and reads the one separator key the keyboard has.** A numeric keypad carries a single
  separator and it is a DOT, whatever the reader's notation is, so under es-AR a graduation typed
  "-2.25" reached the vendor's parser as −225: the dot read as a grouping, and the receta that
  travelled to the taller ground a lens nobody prescribed (nsail#2165). All four boxes pass
  `FigureConverter<T>`, which rewrites a dot to the notation's own decimal separator before the
  vendor parses — so both keys reach the same figure and the box still writes the reader's comma
  back. It rewrites only a dot that cannot ALREADY be read: nothing happens under a notation
  whose decimal separator is the dot, a figure already carrying its decimal separator
  ("48.600,25") is grouped and reads as it stands, and in a notation that groups WITH the dot a
  group — three digits, the figure ending there — keeps the one reading it has, so an importe
  typed "48.600" is still forty-eight thousand six hundred (measured, same class). The vendor's
  `DefaultConverter` is sealed, so the converter wraps one; it is an `ICultureAwareConverter` for
  the same reason that one is, which is what lets a Mud form component fill its `Culture` and
  `Format` from the field's own two parameters — the notation is still said once, on the field.
- **`NsMoneyField` takes `Min`/`Max`, and the bound is the caller's rule** — a screen that
  already knows the range (Autorizar's covered amount, bounded by the order's own total; a
  product's `Price`, floored at zero) says it on the field instead of only after the send. Both
  are **assignment sentinels**, the shape `NsNumericField.Step` set: unset keeps the vendor's
  default, because `TValue` may be a non-nullable numeric with no null to compare against, so a
  null check would read "no bound" as "bound to nothing". The bound reaches the input as
  `min`/`max` **and** as the spinbutton's `aria-valuemin`/`aria-valuemax`, so it is announced and
  not only drawn, and the vendor clamps a figure typed past it rather than handing the value back
  out of range (measured, `NsMoneyFieldBoundsTests`). The field words nothing — the refusal's
  sentence is the sender's, from its own kit's strings, and the server refuses the same range for
  every other caller.
- **A figure in the install's own currency wears no code.** `NsMoneyField` renders `N2` in the
  reader's grouping — character for character what every total, saldo and list cell writes
  beside it — and shows a code only when its `Currency` is set; a figure written as text takes
  the same rule from `NsMoneyText.Of(amount, currency)`. A caller sets it only for an amount in
  ANOTHER currency: `CurrencyRef.ForeignCode` (Accounting) answers the code for any currency but
  the functional one and null for that one, so a document that can be counted elsewhere — a
  settlement, a supplier's price list, a fixed selling rule — hands its currency's `ForeignCode`,
  and every other document hands nothing. Never `Format="C"`: it prints the region's sign
  whatever the amount is really in.
- **`NsNumericField` takes no bound and hands the vendor none.** A measure's range is the
  *message's* rule, like a date's, and the declaration already refuses it at both ends — the
  validator draws the catalog's sentence under the field on the Save, never on the keystroke
  (`PrescriptionRangeAttribute`, Optical; *The required mark is derived, never written* below).
  **Being a `Range` is what lets the bound be READ; only `ICodedValidation` is what makes the
  field SAY the rule** — `RefusalWords` words every attribute from the catalog, so a plain
  `[Range]` is refused with the generic "Fuera de rango" and a coded one says the rule itself.
  The Issue carries the `{from}`/`{to}` the attribute declared, but the house's
  `Problems.OutOfRange` names neither and cannot: the same row answers an `AddOutOfRange(field)`
  minted with no bounds at all, and an unfilled token is left standing literal. **A bound a
  person should READ is said by a row of the kit's own** — scoped
  (`Problems.OutOfRange.{member}`) or under a code of its own — which is the same ladder
  Directory's Característica takes. That is why the
  precedent above is a `Range` *and* coded: the floor is that no field ever draws a member's
  name or a pattern, and a specific sentence is what a code buys on top of it.
  The vendor's `Min`/`Max` are withheld on purpose:
  they **clamp** a typed figure to the bound (measured, `NsMoneyFieldBoundsTests`), which is
  right for money — the caller's own ceiling, agreed before the send — and wrong for a
  graduation, where a slipped +45 quietly becoming +30 is a wrong lens nobody was told about.
  A refusal in words is the answer there; clamping is silence. A product's `MinimumStock` is the
  second caller and takes the same side: -5 clamped to 0 would be a stock alert nobody knows is
  off, so `[NotNegative]` refuses it in words.
- **`NsNumericField` writes a measure ungrouped, with the decimals it really has.** The culture
  above settles the separator; the vendor's *format* defaults to none, so a measure would come
  out of its `decimal(18,3)` column wearing that scale — a minimum stock of ten reading
  "10.000", which is ten thousand in es-AR. The field formats through `NsQuantityText.Format`:
  every decimal the figure really has and no zero it does not. Two constraints decide that
  format and neither is cosmetic. It keeps as many decimal places as a `decimal` can hold (28)
  because **MudNumericField parses the text it wrote back into the binding when the field is
  left**, so a format that rounds the box rounds the value. And it **never groups**, unlike
  `NsMoneyField`: a factura's number rides this same field as a `long`, and "12.345" is not the
  voucher 12345. A quantity written as text takes the same rule from `NsQuantityText.Of(quantity)`
  — the twin of `NsMoneyText.Of` — so a saldo in a list cell, a stock hint and the same figure in
  a field never read as three figures, and a screen never keeps a private reading of its own.
  **The boundary is reach, not the kind of surface**: an app's Web host references that app's
  Shared, so `WorkOrderTicketDocument`'s printed measures take the seam like a cell does. It ends
  at a **kit's** WebApi, which references no component project and spells the measure `"0.##"` of
  its own (`SaleTicketDocument`, `VoucherDocument`, `SaleTicketPdfEndpoint`'s share of an Ajuste —
  the same figure the screen beside it says), and at `BusinessProblem`, whose `"0.####"` lives in
  `NSail.Types`, under the components rather than beside them.
- **A measure whose domain writes it a certain way passes that reading through `Format`.** Unset
  is the plain measure above, which is what a DNP, a segment height and a stock want — the ticket
  the bench reads prints them through `NsQuantityText.Of`, so the box beside it reads the same
  way, and a figure *derived* from two of them (`EyesEditor`'s DNP total) reads by that seam too
  rather than by the scale its own sum happens to carry. A graduation is the other case: it is
  signed to two decimals on every receta in the country, so Editar Receta's sphere/cylinder/Add
  boxes and the cristal's stocking window carry `DiopterText.Format`, and `EyeFormat` and the name
  a lens variant is bred under read through the same member — one notation, one place, whoever
  prints it. What a site passes is a **named domain reading and never a literal format string in
  a page**: a literal is a screen's private reading with extra steps, which is the thing this
  whole rule exists to refuse. A named reading owes the field every decimal its column can hold —
  the vendor parses the text back into the binding — so `DiopterText` may round to two only
  because every diopter column is `numeric(5,2)`.

### Password and color

- **`NsPasswordField` scores what is typed behind `Strength`** — one boolean, never a
  component of its own: the verdict has no meaning apart from the value it scores, so it rides
  the field and lands in the same under-field zone as the hint. Weak/Medium/Strong come from
  `PasswordScore.Measure` (`NSail.Components`, vendor-free and pinned by its own test); the
  bar is hidden while the field is empty, and it moves on the keystroke because every box in
  the family does. **It is advice and never a gate** — NSail has no password policy,
  and giving it one reaches `SetPassword`, the invitation flow and every seeded account.
- **`NsPasswordField` says a secret is on file behind `IsStored`.** A write-only credential never
  travels back, so the box opens empty on every visit and "nothing is stored" looks exactly like
  "the secret is stored and not shown": the flag fills the hint with the Stack's own
  `Components.NsPasswordField.Stored`, which says both that there is one and that leaving the box
  alone keeps it. **False renders no helper node at all**, never an empty one, and an explicit
  `Helper` still wins — the flag only fills a hint the caller wrote none of. It is one boolean
  rather than a per-screen string so no screen copies the sentence by hand; the server side of
  the same contract is a derived `HasXxx` on the settings type ([settings.md](settings.md),
  *A secret is write-only*) and a save that reads a blank field as "unchanged".
- **`NsColorField` takes a typed hex, not only a dropdown pick.** The box holds a plain
  6-digit hex — no alpha channel — matching what every caller already stores; text that
  isn't a color never reaches the model. *Accepted debt: the box itself keeps showing the
  refused text on screen — the vendor never calls back into `TextChanged` for text it
  rejects, so nothing tells the field to redraw over it.*

---

## What the box holds is what the form submits

**A text-shaped box commits every keystroke, and there is no knob that says otherwise.**
`NsTextField`, `NsTextArea`, `NsEmailField`, `NsPhoneField`, `NsUrlField`, `NsPasswordField`,
`NsSearchField`, `NsNumericField`, `NsMoneyField`, `NsPercentField` and `NsDurationField` each
hardcode the vendor's `Immediate` on. With it off the input carries **no `oninput` handler at
all** and the value reaches the model on `onchange`, which for a text input is the blur: a
Nombre typed and Guardar pressed without leaving the box is refused as `Obligatorio` under a
field visibly holding the text, and the second press saves it (nsail#1937). There is no knob
for the other answer — a field that commits late cannot be written.

- **The parameter does not exist, so neither does the mistake.** It was dropped from
  `NsFieldBase` rather than flipped — a knob nobody may set to the wrong value is a rule
  enforced by a review ([principles.md](../principles.md), *Remove the possibility*). It was
  dead on `NsSelect` and `NsAutocomplete` anyway: a select's value comes from a pick, and the
  lookup binds the selected ITEM, not the text its search is typed into.
- **The vendor wires `oninput` INSTEAD of `onchange`, never both.** For a text input this is a
  superset of the gestures — typing, paste, cut, undo, autofill and the numeric spinners all
  raise `input` — and `onblur` still runs, so a figure still formats when the box is left. What
  it costs is a test: bUnit's `Change()` now names an event the element has no handler for
  ([testing.md](../testing.md)).
- **A figure box is safe to hear every keystroke, and a date box is not.** A decimal is typed
  through a state that reads as a whole number, so what matters is that the vendor keeps drawing
  the half-typed separator rather than reformatting over it — measured, in both notations the
  house reads. The date boxes commit on the leave instead, and that is a rule rather than an
  omission: `MudPicker.ImmediateText` is their version of the same knob and it stays off,
  because `DateText` reads `15/8/2` as the year 2002 — a picker hearing every keystroke would
  commit a real wrong date and redraw the box over what is being typed (the round-trip trap the
  *Dates* section above measures in full). `NsColorField` is on the leave for the same reason: a
  half-typed hex parses as a real other colour. Those three — `NsDateField`, `NsDateTimeField`,
  `NsColorField` — are the whole exception, and they are the pickers.

### `ValueChanged` answers what the value IS; `OnCommit` answers that the person is DONE

**A handler that takes the value somewhere else, or sends it, hangs on `OnCommit` and never on
`ValueChanged`.** The binding moves on the keystroke by the rule above, so a handler on it runs
once per character: a chip list took one mailbox per letter and spent its cap of three on
`a`, `n`, `a@`; a price cell typed from 1500 to 2350 published 2, 23 and 235 on the way, each a
real price; a barcode row sent thirteen saves for one EAN (nsail#1937). `OnCommit` is raised
when the entry ENDS — Enter pressed, or the box left — carrying what the box holds.

- **It lives on `NsTextFieldBase`, which only the typed family inherits.** Not on `NsFieldBase`:
  a select's and a lookup's value arrives whole from a pick, and the pickers above commit on the
  leave already, so a knob there would be one that means nothing
  ([principles.md](../principles.md), *Remove the possibility* — the same reading that deleted
  `Immediate`).
- **Enter is not an ending in `NsTextArea`**, where it is itself text (`CommitsOnEnter`).
- **A field that TAKES the text takes it at the end of the entry, and the submit is not that
  end.** The heading above is about a box whose value IS the form's — a chip list's entry box is
  not: what it holds becomes a chip, and until it does, the submit saves without it. So
  `MailCopyAddressesField` takes a mailbox on Enter or on the box being left, so an address still
  standing in the box when Guardar is pressed rides on the click's own blur landing first — the
  very race this section exists about. **It is the design and not a gap**: a half-typed mailbox is
  not a mailbox, and taking one per keystroke is what spent the field's cap of three on `a`, `n`,
  `a@`. A new field of this shape owes the same reading, and owes the person a visible gesture —
  a chip appears — rather than a silent save.
- **It is inert without a handler.** Every field in the house that only binds is untouched by
  the seam: the leave and the Enter stay the vendor's own business there.
- **It carries what the box holds, and an ended entry is not remembered.** The text typed is
  kept only because a box bound by `Value` alone — `PolicyConstraintsEditor`'s reference id,
  which reads the text only once it parses — never has the keystroke written back to it, so
  `Value` is not what the box has. It is dropped the moment the entry ends: the vendor text
  boxes, unlike the pickers, say nothing when a value is pushed INTO them, so a member the
  screen moves itself reaches the box unheard, and a field still holding the old text would
  commit it on the next leave — a value nobody typed, over one somebody did.
- **A bUnit suite proving a take or a save acts through the end of the entry** — `Input(…)` then
  `Blur()` or `KeyDown(Key.Enter)` — and `AwaitedActTests` gates those two acts like the other
  four ([testing.md](../testing.md)).

---

## The required mark is derived, never written

**A field whose bound member declares `[Required]` marks itself, and no page says so twice.**
`NsFieldBase` reads the attribute off the member the binding expression already names for the
label, so the asterisk cannot drift from the declaration it is about — no screen marks three of
its required fields and leaves the fourth bare.

- **The mark reads exactly what refuses.** The derivation is scoped to the `EditContext`'s own
  model **plus whatever a member opened to it** (`[Validated]`, messaging.md) — which is what
  `NsDataAnnotationsValidator` validates and what `MessageValidator` walks on the wire, from one
  reading (`ValidatedModels`). A `[Required]` on a **row** model nothing opened — a sale line's
  `Description`, a channel's `Value` — is enforced by neither end, so it is neither marked nor
  refused; a model a message opened is refused at both ends and marked like the message's own.
- **The same reading answers for a bound, not only a mark.** `NsFieldBase.Declared<T>()` hands a
  field whatever attribute its bound member declares, under that same scope; `NsDateField`'s
  calendar ceiling is the one caller today. A field that draws anything from a declaration draws
  it from the declaration that will refuse.
- **Only an annotation that CAN refuse is derived from.** `RequiredAttribute` passes any non-null
  boxed value, so on a **non-nullable value type** — `[Required] int Number`, a `bool`, an enum, a
  `Guid` bound as `Guid` — it is already satisfied at the type's own default and refuses nothing at
  either end. `RequiredMembers` derives the mark only for a reference type or a `Nullable<T>`;
  those other members are marked by the `Required` parameter or not at all, since an asterisk no
  validator will honour is the same lie as a refused field left bare (`NsFieldRequiredMarkTests`).
- **`Required` survives as the override, for what an annotation cannot express**: a requirement
  that holds only sometimes (`CreateStorePage`'s `Required="@(_model.Kind == StoreKind.External)"`)
  and a non-nullable binding whose "empty" is a sentinel `RequiredAttribute` never rejects (a
  `Guid` bound as `Guid` — `Guid.Empty` boxes non-null). It is also the only half that feeds the
  field's own client-side required check — which is what keeps the mark and the refusal minted
  together on exactly the types the annotation cannot see; on the derived half the validator
  already refuses, and adding it there would only draw the same word twice.
- **The first Save raises that own check; from then on it tracks the value.** A form nobody has
  submitted draws nothing — a create form opens clean though every required field in it is empty,
  because the refusal belongs to Save and not to the keystroke (intentional-ui.md, *Refusal
  placement*). Once the form HAS asked, the field follows its own answer without being asked
  again: a value landing in a refused field takes the message back on the spot, and emptying a
  field that was answered puts it back, neither waiting for a second Save. The field re-asks its
  store on the parameter set the binding's write triggers, which is the first moment the new
  value is visible and the only one that also catches a value a SCREEN wrote into the model behind
  the field; a seam hung off the pick alone would miss that one. **The declared half keeps the same
  gate, and its moment is the form's own**: `NsDataAnnotationsValidator` hears every field change —
  it must, since the dirty flag that lights Guardar rides that event — and revalidates the member
  only once the form has requested validation once. A box commits on the keystroke (*What the box
  holds is what the form submits*), so without that gate a CUIT was refused from its first digit,
  a mailbox until its `@domain` closed and a figure box while it stood empty to be retyped
  (`NsFieldRequiredLiftTests`, `NsDeclaredRefusalMomentTests`).
- **`For` names the member for NSail and never reaches the vendor.** It is how a field bound by
  `Value` plus `ValueChanged` — a shared editor's, a row cell's — says what it is bound to, and the
  label, the mark and the refusal's field identifier are derived from it. Handed on to the vendor
  control it is a second refusal moment the house does not own: `MudFormComponent` reads the
  member's `ValidationAttributes` off that expression and refuses them on every value change,
  staging the attribute's own **English** into the same `EditContext` — the untranslated text
  `NsDataAnnotationsValidator` exists to replace, under a box still being typed into. The figure
  family never passed it; none of them do now.
- **A refusal leaves the screen with whatever raised it.** Anything holding a
  `ValidationMessageStore` of its own — a field, a product type's tab — empties it before letting
  go, on dispose and on a rebind to another form. The store stays registered in the
  `EditContext`'s field state after its owner drops it, and `Validate()` counts the messages of
  every store there, so a refusal left behind by a field "Soy yo" took away, or by the extension
  tab a type swap discarded, holds every later Save and the gate returns mute: a clean, complete
  form and a dead button, which is the worst failure there is (principles.md). The same clear is
  what lets a field come BACK clean — it owes the form nothing until the next Save, so an orphan
  message is all it would have had to draw (`NsFieldRefusalLifetimeTests`).
- **A page that repeats what the model declares is dropping a line, not keeping a belt.** The
  requirement is declared where the refusal lives — on the message — and a rule the handler alone
  knew is moved there rather than marked by hand (`CreateSale.PartyId`).
- **The mark answers for the SCREEN, and a screen may ask for more than the wire.** What it may
  not do is mark a field it will then accept empty. A screen that hides the required member —
  `RegisterPractitionerForm` composes a `[Required]` `DisplayName` out of `Nombre`/`Apellido` and
  renders none of it — marks the fields that compose it and refuses them there, because the
  hidden member's own refusal has nothing to draw under and would stop the submit in silence
  (measured). A screen that renders the required member marks that one and leaves the fields
  feeding it alone. **Where one of the composing fields already suffices, only that one is
  marked**: `PartyIdentityEditor` composes the display name out of `Nombre`/`Apellido` wherever
  the kind is chosen and marks `Nombre` alone, because a name alone composes a name — a mark on
  `Apellido` would be one editor imposing on all six doors that render it a refusal not one of
  their messages carries, which is the other half of the same rule. **That ceiling is a SHARED
  editor's, not a form's**: `RegisterPractitionerForm` marks both names and is right to, because
  it answers for one message and may ask that message for more. Two doors of the same alta can
  therefore differ on the asterisk — each is answering for its own screen, which is what the
  rule says the mark does.

---

## The label floats on the value, never on the text the field resolved

**A field that HOLDS an answer floats its label, whether or not it can read that answer as words
yet.** The vendor decides the label's rest position from the box's own TEXT, which is right for a
field whose text IS its value and wrong for the three that RESOLVE one: `NsSelect` and
`NsMultiSelect` name their value out of `Items`, `NsAutocomplete` out of the row `OnLoad`
fetches. A wrapper that fills either from a read — `AgendaLocationSelect` and the two dozen
pickers shaped like it — therefore holds its value for at least one render before it can name
it, and a label left at rest is a label standing where the name is about to be painted: the
words land under it and the label then travels off them.

- **The declaration is `NsFieldBase.HoldsValue`** — read by `NsSelect` and `NsAutocomplete`,
  and answered by `NsMultiSelect` off its own selection, since a multi-valued field's value is
  a set rather than a `TValue`. A binding left at its default (null, `Guid.Empty`, 0) is a
  question nobody answered, so the empty create form still rests its label inside the box.
- **It only ADDS an arm.** A value the list already names floats on the text as before — a null
  an option NAMES ("Sin lugar") is an answer like any other — and so does a field carrying a
  placeholder.
- **Not CSS.** `ns-mud.css` restates the vendor's shrink geometry once, under
  `:-webkit-autofill`, and that is for a state C# genuinely cannot see: the browser writing a
  saved credential straight into the DOM. A state the component knows is declared in C#.
- **A permanent suffix is the one shape the derivation cannot reach, and it floats the label
  regardless of value, text or focus.** A `Suffix` or unit prints exactly where a long label
  would otherwise rest at empty, unfocused rest, and no value is coming to lift it. Two shapes
  answer it, and the difference is who owns the suffix: `NsTextField.ShrinkLabel` is an **opt-in
  parameter** where the CALLER composes the suffix (`MailAddressField`, cases), and a caller
  reaches for it only once widening the field or shortening the label is not an option, never to
  fix a label a value's own derivation would already float; where the suffix is the COMPONENT's
  own and therefore permanent on every instance, the component floats always and no caller is
  asked — `NsDurationField` wears its unit on every instance, so it has no empty state a label
  may rest in and nothing is left for a screen to get wrong.

---

## The message: a field reserves nothing, and what arrives is read whole

**A field reserves no line for its refusal.** MudBlazor renders the helper container only when
there is text in it, so a form nobody refused spends no height on the possibility. **The message
that does arrive stands in normal flow at the foot of the field's own control box** — real
height, honest wrap, nothing laid out over anything — and the row it grows pays for it.

- **It wraps to as many lines as its sentence needs, at rest.** No hover, no focus, no tap, at
  every width: a reveal gesture answers the pointer and abandons the phone, which is the surface
  where every field is narrow (the same reason `NsHelp` opens on click). A refusal clamped to its
  field's width reads as its own first half, and the half it loses is the one that says what to
  fix.
- **The growth is downward and only downward.** The vendor lays `.mud-input-control` out as a
  flex column with the helper container as its last item, so the input's own top does not move;
  `.ns-grid` is `align-items: flex-start`, so the fields sharing the line keep their tops flush
  with it. Nothing above the row shifts — what shifts is what stands under it.
- **The grid reserves no foot.** A message in flow grows the grid that holds it, so nothing hangs
  past its bottom edge for a panel's scroller to clip or grow a scrollbar over.
- **The house styles the message nowhere.** Everything a stylesheet could say about it is said by
  not saying it: a class or a rule arriving on that node is a clamp coming back
  (`NsReservedGeometryTests`). A box wider than the field is refused for the same reason it
  looks tempting — it could only be drawn over the field beside it, and a message occluding the
  control the user is being asked to fix answers worse than the clip it replaces.
- **One message per field, however many rules it broke** (`NsFieldBase.GetErrorText` takes the
  first the `EditContext` holds): the strip answers one question at a time, and the length a
  refusal may spend is its own sentence's, never a list's.
- **A hint yields the strip whole, box and words.** A field carrying both `Helper` and a refusal
  shows the refusal and nothing else: both stand in flow, so a hint kept invisible would hold an
  empty line open beside the very message it stepped aside for. It costs no movement either —
  `.mud-input-helper-text` and `Typo.caption` are the same `.75rem/1.66` box at the same 3px
  offset, so a one-line refusal lands exactly where the hint stood.
- **Whoever posts into the store words it from the same ladder.** `NsDataAnnotationsValidator`
  is not the only one: a component holding a model the form's own validator does not reach
  (`ProductTypeEditor`, a product type's tab) validates it itself and adds to a
  `ValidationMessageStore` of its own. The words come from `RefusalWords` (`NSail.Components`),
  which is that validator's own ladder: the failing attribute is handed to
  `MessageValidator.IssueFor` — the one switch that words the wire's Issue — and the catalog
  answers that code, scoped row first. An absent row shows the KEY, never the attribute's own
  English, which a screen has no sender to quote. A store that posts `result.ErrorMessage`
  straight draws the attribute's English on an es-AR screen (measured).

### What a screen reader hears

- **A screen reader is handed the words by a second, invisible node.** The container the eye
  reads is named by nothing, and naming is all an assistive technology goes by. `NsFieldHelper`
  renders the refusal again inside `.ns-field-described` — clipped out of sight but left in the
  accessibility tree — and the field hands its own input the id of that node to be described by,
  so the input announces WHY it is invalid instead of only that it is. **The words being in the
  DOM twice is not a second placement**: one node is the report, the other is never read by an
  eye and exists only to be pointed at. Visually hidden means *hidden from the eye, present to
  the tree* — `display:none`, `visibility:hidden` and `aria-hidden` each remove it from the tree
  too, and a reference into any of them announces strictly less than silence.
- **The reference rides in `NsFieldBase.InputAttributes`, splatted onto the input with
  everything else the field splats** — the autofill hint, the accessible name, the mostrador
  focus. Every shape reaches its own input the same way, the four `MudPicker` shapes
  (`NsDateField`, `NsDateTimeField`, `NsTimeField`, `NsColorField`) included: a picker composes
  a MudInput it does not hand parameters to, and the splat travels through both layers
  untouched. The vendor's own `HelperId` is not used — on the error path it stamps the id onto
  **no element at all**, so the words it draws stay unnamed either way and the house has to
  render the node it points at regardless. Both measured, not read: `NsReservedGeometryTests`.
- **A shape that splats it renders the node it names.** `NsCheckBox` gets the described node and
  no hint — a box and its label are one line and a hint under them would be a second — because a
  reference into a node that is not there announces strictly less than the silence it replaces.
  `NsField`, which drives no input of its own, carries the reference on its control box instead.
- **The collection is read at render, never refreshed when a parameter is set.** A refusal
  arrives from the `EditContext`, which re-renders the field without setting anything on it, so
  attributes built at parameter time would still be describing the clean field.

---

## Lookups

Which combo a screen uses — full or `Lazy` — is the core doc's rule, not this page's. "Full" is
a `*Select`, or a non-`Lazy` `NsAutocomplete` where typing to filter still helps.

- **The open list and the closed field are allowed to say different things, and `ItemTemplate` is
  where that is declared** — on `NsSelect` as on `NsAutocomplete`. `ItemText` names the value and
  so names the CLOSED box; `ItemTemplate` draws the option. A figure the operator chooses ON — a
  balance, a count — belongs on the option alone: a field that kept it would go on saying a
  number from whenever it was picked (`ProductVariantLookup`).
- **The end adornment is a button and carries a name** (`AdornmentAriaLabel`): the chevron that
  opens a browsed lookup's list. It is icon-only at every width, so it takes the ungated whisper
  `ns-square` takes ([actions.md](actions.md)).
- **A `Lazy` lookup draws no adornment.** Its list is what a term answers, so a button offering
  to open it has no gesture: pressed, it focused the box and opened nothing. What the field is
  for is said by the word in it — the `Buscar {concept}` placeholder — which reads at every
  width where a glyph never did. The (x) is a different control and `Clearable` keeps it.

- **A picker answers to the form's writability the way a field does, and reads it off
  `NsPickerBase`.** A `*Lookup` or a hand-rolled `*Select` is not an `NsFieldBase` — it
  *composes* one — so the box draws read-only under an `NsForm ReadOnly` on its own while the
  picker's own `ReadOnly`/`Disabled` parameters stay whatever the screen wrote, and everything
  gated on them goes ungated. So the pair lives on `NsPickerBase` together with the same
  cascade read and the same `IsReadOnly`/`IsDisabled` derivation `NsFieldBase` gives a field:
  a picker gates on those two and never on the bare parameters, and a picker on `NsPartial`
  inherits that base rather than declaring the pair itself.
- **A lookup asks two questions and they never share a Runner.** The SEARCH answers "what can
  I pick" and the RESOLVE (`OnLoad`) answers "what does the value I am already holding read as"
  — two independent reads that overlap the moment either costs a round trip. Sharing one Runner
  loses both ways: a resolve starting under a live search throws out of `OnParametersSetAsync`,
  and a search starting under a live resolve cancels it after the field recorded the value as
  asked, leaving a box that can never fill again. The same split, and the same reason, as
  `NsPage`'s own load Runner ([surfaces.md](surfaces.md)). **A cancelled resolve files no
  answer** — it learned nothing, so the mark that says "asked" comes off with it.
- **The SEARCH half of the same rule: an out-of-order answer never wins the vendor's own last
  write.** Two searches can be in flight over real latency, landing in whichever order the
  network hands them back rather than the order they were asked in, and the vendor redraws its
  list from whatever a superseded call returns with no regard for that order. A term compare
  alone catches a call for a term the box has moved past, but not one for a term the box has
  typed FORWARD AND BACK ONTO AGAIN while that same-termed call was still out cancelled — so a
  search answers with the field's last good read, never its own, once EITHER the term no longer
  matches the box or its own cancellation fired.
- **The open list is sized by its RESULTS** — floored at the field it drops from, bounded by a
  measure, never capped at the box the field stands in. The vendor caps its popover at the
  anchor's own width by default, and in a line editor the anchor is a table cell, so one word at
  the tree's one `MudAutocomplete` answers for every lookup and no caller says anything
  (`RelativeWidth`). A reading past the measure **wraps** — the whole option stays on screen —
  against the 28rem the row's text box carries in `ns-mud.css`: the aside's own content width,
  the narrowest surface a lookup opens on.
- **A grid cell holding a lookup that declares `Grow` asks for that basis at every width, and
  the ROW is what answers whether it can be paid**, since an auto-layout table otherwise leaves
  that column whatever the others did not take and the chosen row's SKU stops reading. Both
  halves of that sentence are load-bearing: a lookup declares `Grow` when its reading is long,
  and a minimum a row cannot pay for is *added* to the table rather than redistributed inside
  it — which takes the row's own confirm past the edge. So the cell **demands** the basis from
  `md` up (a `min-width`, which the container can always pay for there and which keeps the
  column growing past the basis with the surplus) and **asks for nothing below it**, where the
  open row is no longer a row of columns at all: it stacks, and a card line is already the
  widest a field can be ([hosts.md](hosts.md)). The container picks whether the cell asks; it
  never decides what the row is made of (cases).
- **Enter over an open list belongs to the list, and the form never sees it.** A text box inside
  a form implicitly submits it on Enter — the browser's own default action, on the KEYDOWN — and
  the vendor picks the highlighted option from the keyup after it, so without a guard every
  lookup standing in an `NsForm` would save the whole document on the keystroke that was only
  choosing an article. The prevention is a document keydown listener in capture (`ns.js`,
  `onDocumentKeyDown`) and not markup, for `NsLink`'s reason: `@onkeydown:preventDefault` is
  decided when the element renders, and whether an Enter deserves preventing is a property of the
  keystroke — whether a list was open at THAT instant. What it reads is the box's own combobox
  state, which the vendor marks expanded while the list is open and, on a lookup, **only while it
  has options**: a closed, empty or already-settled box keeps Enter for the form, a field that is
  not a combobox (a price cell, the till's Fondo inicial) still saves on Enter, and a button that
  carries the same attribute — an expander's chevron, a menu's face — keeps its own activation,
  since the guard reads the element the keystroke landed on. The same rule covers `NsSelect` and
  `NsMultiSelect`: the vendor stamps both words on their own input too, so nothing in the guard
  names a component (`NsComboboxExpandedMarkTests` holds all three).
- **A row the rules cannot settle alone opens its own second level, inside the list.**
  `ItemTemplate` seats an `NsMenu` with a `Content` face on that option, and the same row is
  also handed to `ItemDisabledFunc`: the face IS the row — `.ns-lookup-list` makes the trigger
  span it, and the menu's own activator stops the click — so a click anywhere on the reading
  opens the answers instead of picking past them, and the whole choice stays one gesture. But the
  vendor's own keyboard selection never looks at `ItemTemplate` at all — arrow navigation and the
  index Enter confirms pick straight from the item list, past the menu — so a row that wires only
  the template still falls, unasked, to the keyboard. `ItemDisabledFunc` on that same condition
  is what the vendor already excludes from both, and `ns-mud.css` gives the row back its mouse
  affordance and reading, which the vendor's disabled styling would otherwise mute. The value
  then arrives from OUTSIDE the field's own pick, which is exactly what the belt in
  `OnParametersSet` closes the open list for. The core doc's rule is that such a question lives
  here and never under the field. **A field nobody may write to asks nothing**: `ReadOnly` and
  `Disabled` govern the row's second level too, not only the box — the contract is the picker's,
  and moving the question into the list does not hand it to the list.
- **The prompt is composed, never written per entity.** With no translated member key,
  `NsAutocomplete` falls back to the item type's concept (`Metadata.KeyFor(typeof(TItem))` —
  "Directory.PartyRef" → "Personas") formatted into the Stack template `Common.SearchPrompt`
  ("Buscar {0}" → "Buscar Personas"). A specific member key still wins. There is no
  `*.Search` key family and no suffix anywhere.

### The create entry

The rule — **creation lives inside the dropdown, never beside the field** — is the core doc's.
Its contract: one entry, the dropdown's **permanent last item**, in every state (before typing,
with partial matches, on empty results).

- The entry is gated by the destination page's own authorize attributes, through the same
  single gate as the nav menu.
- **It is not offered on a picker nobody may write to.** `NsLookupBase.CreateRoute` is null
  wherever the lookup reads itself read-only or disabled, so the dropdown's entry, the
  `QuickAdd` escape hatch and the empty set's door below go together — one sentence, one gate,
  and no facade repeats it.
- Its label is composed (`Common.CreateNew` + the concept), never written per entity.
- Header fields and line-editor rows alike, zero per-row chrome.
- **It is never smaller than the rows it follows** (cases).
- **A lookup adopts the save it asked for, and no other.** The entry's href carries a token the
  lookup minted (`source=`); the create page holds it from its query and every event it
  publishes wears it (messaging.md, Headers), so two fields of one concept on one screen never
  both take the row either of them created. **A save that names nobody — a grid's own create
  page, a handler's event — is adopted by no lookup at all**, deliberately.
- **The adoption counts as editing the field.** The round trip lands on the same page the
  screen routed away from, and that page's own `EditContext` must learn the field changed —
  the same notification a click on a real option gets from `NsFieldBase.SetValue` — or the
  form's own submit never enables.

### The list's third entry, the caller's own

`NsAutocomplete.Offer` is a fragment a facade may seat where the `QuickAdd` button sits, and it
answers the case `QuickAdd` cannot: **an offer about the text in the box whose answer needs a
question one send does not ask.** The counter's scanned code is the case it was built for — a code
matching nothing is taught by naming an article AND one of its units, which is two questions
(`AvailableProductLookup`/`BarcodeLinkMenu`).

- **The Stack decides WHERE, the caller decides WHEN.** The entry renders with the list's rows and
  without them — what it is about is the text, not the emptiness — and wears the same geometry the
  create entry restates: a caller brings content, never padding. Whether there is anything to offer
  is the facade's own answer, because only the facade knows what its search came back with: a
  fragment that renders nothing leaves the list as it was.
- **The text it quotes has to still be in the box.** The Stack withholds the entry while the box is
  empty and while the search for what is in it has not answered — `QuickAdd`'s own two conditions,
  and this is the entry that WRITES: a `Lazy` field emptied never reaches the facade's `OnSearch`
  at all, so what the facade computed for the last term it searched would otherwise stand as an
  offer to write a code nobody can read on screen any more.
- **It is the FACADE's render, so the facade renders it.** The field redraws its own list off what
  the search returned, and that is not a render of the component that owns the fragment — a facade
  setting state inside its own `OnSearch` calls `StateHasChanged` or the entry stays as it was
  drawn before the search answered.
- **The offer answers with a WHOLE value, or it has no business being an offer.** What the caller's
  second level hands back is one of the rows this field's own search would have returned — the
  offer's own list is that search, narrowed the same way — so the pick sets the value in place and
  the field asks nothing more. The alternative, re-running the search after the act and letting
  the operator choose again, needs the vendor to redraw a list it has already answered, which is
  a seam the Stack does not own (`MudAutocomplete.OpenMenuAsync` refreshes only a list still open,
  and nothing here can promise it is). Where the row the offer got back is still missing an answer
  the field's own option would have asked for — a price only a supplier settles — the facade draws
  THAT option in the entry the offer just vacated rather than handing the value over half-made: the
  entry is a seat in the open list, so the question left standing is asked where the operator is
  already looking.
- **The writability sentence is the same one**: an offer is a way in, so a picker nobody may write
  to renders none, exactly as it renders no create entry and no `QuickAdd`.
- **The refusal is the offer's own to catch, not the ambient toast's.** The click that runs the
  write closes the entry's own nested vendor menus on its way out (`BarcodeLinkMenu`'s article,
  then its unit), so a refusal left to `Surface.OnProblem` rides a teardown the operator has
  already stopped looking at. The offer catches its own `BusinessException` and answers with an
  `Alert`, the same way `ConfirmSend` answers a refusal with no field to anchor under.

### The door, for a set that is empty

`NsMissing<TItem>` — the one place creation leaves the dropdown, because a dropdown with no rows
is a place nobody opens (core doc's rule and its exception; cases). Contract:

- **Rendered by the picker, not by the screen.** A picker that preloads a short list knows both
  halves — that the reading came back, and that it came back empty — and draws the door under
  its own field. `StoreSelect` and `TenderMethodSelect` carry it today; any picker with a create
  route adopts it in three lines. A screen writes nothing.
- **`CreateRoute` is the raw app route** and nothing else: the same shape
  `NsAutocomplete.CreateRoute` takes, gated by the destination page's own attributes through the
  same single gate, resolved at the anchor against the surface the field stands on.
- **Both sentences are derived.** The concept is `Metadata.KeyFor(typeof(TItem))` — the same key
  the search prompt and the create entry read — and the act is `Common.CreateNew`; the fact is
  the Stack template `Common.NoOptions`. A type whose concept nobody translated renders
  **nothing at all**, rather than a sentence with a hole where the name goes.
- **Only while it is true.** Not before the reading returns (empty is then "not yet"), not on a
  read-only or disabled field, and not for a session the gate would refuse the door to.

---

## Files

**One file is one field; a set of them is a different field.** `NsFileUpload` holds ONE value
(`@bind-Value`, `Guid?`); `NsMultiFileUpload` holds an ordered set (`IReadOnlyList<Guid>`).
They take **the same parameters** — the list field's `Value` is simply the list — and `Preview`
decides whether what is held is shown. **No parameter turns one into the other** (cases).

- **A field line carries exactly ONE action icon.** Without a preview it is the upload trigger
  while empty and the **clear** once filled (replacing is clear-then-browse). **With a preview
  the image is a second surface**: the clear moves onto it (top-right, circled) and the line
  keeps the trigger, so replacing is in-place.
- **Picking over a value REPLACES it.** The single field never holds two, never lists, never
  shows a second clear.
- **The presentation is the house's, all of it.** The vendor is the picker and nothing else;
  its selected-files template is suppressed. Reference case for **no `Mud*` chrome leaking
  through an `Ns*` component**, pinned by class name in the component tests.
- **Multi always shows the trigger.** Without a preview the files are a list, each line with
  its own clear; with one they are thumbnail cards, each with its own clear. One selection is
  one edit.
- **The order IS the value.** A multi field with a preview reorders by drag and drop and raises
  `ValueChanged` with the new order. **The cover is `Value[0]` — there is no cover flag, ever.**
- **The list field is founded by its consumers, never in advance.**
- **Deletion rides the SAVE.** Clearing removes an id from the value and nothing else; the
  submit carries the final set and its handler diffs. *Accepted debt: an abandoned form leaves
  an orphan asset; no sweep exists.*
- **Empty states show the shape of what will land there** plus the trigger, so the box does not
  change size when the first file arrives — the dashed frame carries `NsIcons.Image` as an
  **illustration**, the one case `ns-mud.css` sizes a glyph outside the three steps.
- **`MaxSize` defaults to 5 MB and is per control**; a file over it is refused *on the field*
  with the limit in the sentence, never dropped silently. `Accept` is per control.
- **Where bytes go is `IFileStore`, not the field's business.** The app's registered store
  answers for ordinary screens; a screen whose files belong to somebody other than the
  session's organization hands its own `Store`. A store that cannot build a preview URL from an
  id answers null and the field shows no image.
