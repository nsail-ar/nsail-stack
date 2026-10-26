# Exporting

Every list can be read out as a file. The capability is one seam in two halves: a
**format-neutral table** anything can build, and a **writer** that turns one into a file. Neither
half knows the other, which is what makes a third format a project and not a change to the grid.

---

## The table model

`NSail.Exporting`, and it is four nested records plus a format:

```
ExportBook   the file's own name (no extension) and its pages
ExportPage   one sheet, named: the tables that go on it
ExportTable  the columns, and the rows as IAsyncEnumerable
ExportRow    one row's values, positionally against the columns
ExportFormat Text | Number | Money | Date | Percent
```

**`ExportBook.Of(name, table)` is the shape nearly every caller wants** — one page, one table —
so no screen writes the two wrappers.

**The format lives on the COLUMN and nowhere else.** A value carrying its own would be the same
decision in two places, and a row that disagreed with its heading is a file no spreadsheet can
make sense of.

**`ExportTable.Rows` is an `IAsyncEnumerable` and may be enumerated ONCE.** The rows of a real
list arrive a page at a time from somewhere that has to be awaited, and a writer that pulls them
one at a time never holds the list — tens of thousands of rows cost the file and the row in hand.
A writer that needed two passes would ask a server for every page twice.

**Values are VALUES, never their printed form.** A `decimal` reaches the writer as a decimal and a
date as a date; a caller that formatted them first has handed every writer text and left the
spreadsheet nothing to total.

---

## The writers

`IExportWriter` announces its `Kind`, its `Extension` and its `ContentType`, and writes a book
into a `Stream`. **The culture is handed in, never read off the thread**: the file is for the
person who asked for it, and on a server that person is a request and never the process.

**What each writer does with that culture differs, and the difference is not a style choice.** A
spreadsheet stores invariant values and the region picks only the date *pattern* — grouping and
the decimal mark are resolved by whoever opens the file, so writing them in the author's would be
the figure spelled twice and the cell would arrive as text. A text format has no display layer at
all, so it prints every figure in the reader's own separators.

| | `.xlsx` (`NSail.Exporting.Excel`) | `.csv` (`NSail.Exporting`) |
|---|---|---|
| Cells | typed — a number stays a number, a date a date — with a per-column number format | text, in the reader's separators |
| Pages | one sheet per table, named after the table or its page | flattened in order, a blank line between tables |
| Encoding | OOXML | UTF-8 **with a byte-order mark** |
| Separator | — | derived from the decimal mark: `;` where it is a comma, `,` otherwise |

**The CSV separator is derived and never configured.** A culture that writes `1.234,56` cannot
separate fields with the comma inside its own figures; reading the separator off the region
instead would be the same answer from a value that may be tailored per machine, and a mismatch
between the two is a file that opens as one column.

**`.xlsx` is MiniExcel (Apache-2.0).** It is the one candidate whose writer pulls rows from an
`IAsyncEnumerable` instead of building the workbook in memory first, and on `net10.0` it carries no
transitive dependency — which matters because the grid that offers the export runs in a browser.
**Its `FastMode` is not used despite the name**: it buffers the sheet to seek a dimension back into
it, so what it costs grows with the list. And **its `Culture` is not set**, which makes it print
every value and hand the cell over as a string.

**The split between the two projects is the dependency boundary, not a layer.** `NSail.Exporting`
costs nothing and carries the model, the registry and the text writer; the spreadsheet library is
`NSail.Exporting.Excel`'s, so a surface that only ever wanted the text file does not carry one.

**What an install can produce is what it registered.** `AddExporting()` plus, where the host wants
it, `AddExcelExport()` — an app's own composition, beside `AddNavMenu`. `ExportWriters` is that
list, **offered in `ExportKind`'s order rather than registration order** (which format reads as the
obvious choice is one decision, not each host's), and chrome lists it: a format nobody registered
is never offered rather than offered and refused.

---

## What a list exports

**A grid whose rows are READ offers Exportar, and no screen asks for it.** A screen that had to ask
for Exportar is a screen that can forget to.

**The trigger is an icon, never text, and it rides the page's header — ahead of Nuevo, never
below it — for the page's own, UNNAMED list only.** A grid with its own `Title` is a panel of a
bigger page and keeps its export beside that name, in its own `.ns-collection-bar`, no matter
what else stands on the surface; an unnamed grid is a candidate for being the page's own list, so
it hands its export up to its `SurfaceContext` instead (`AnnounceExport`, the same crossing
`Title` already makes the other way: a table reads the page's own name, a header reads the
table's own control), and `NsPageHeader` draws that export's trigger in its toolbar, before `Actions`, where it
claims the slot (`SurfaceContext.ClaimsExport`, asked from its own render, first header wins). An
unnamed grid whose surface has no header to claim the slot — an embedded grid, a dialog — keeps
drawing its own, same as a named one. First unnamed, exportable grid to announce on a surface
wins the slot the same way (`SurfaceContext.AnnounceExport`): a second grid's render, exportable
or not, is simply not read while another grid holds it, and a grid's own teardown hands the slot
back. The bar still draws for a grid that has a control of its own to put there even where
nothing named the grid, because the pager row is the only alternative seat and it exists in one
of the three paging modes. The icon is `NsIcons.Download` and the word `Exportar` is its tooltip
and accessible name, never visible text, at any width — `NsMenu`'s own `Icon`+`Label` already
whisper both through the house's one bubble, so nothing wraps it in a tooltip of its own.

**The slot changes hands out loud, both ways.** A header's own teardown hands the claim back
(`SurfaceContext.ReleaseExport`) and a free slot taken says so too, both on the event the grid's
own announcement already rides (`ExportChanged`): the grid draws its trigger in its collection
bar again the moment no header holds the seat, and drops it the moment one does, instead of
waiting for a render nothing on the page has a reason to make. Why the word is owed at all, and
how much either side may redraw for it, is the announce seam's — [ui/surfaces.md](ui/surfaces.md),
where this claim is named beside the row's.

**The announcement carries the ACT and not only the trigger** (`SurfaceExport`: the fragment
chrome draws, the `ExportKind`s this install registered, and `Run`). The trigger is for the person
in front of the list; `Run` is for whoever asks for the same file without pressing it — an agent
sent for "my cobros since June" — and what it writes is the same file by construction, because it
is the same grid, the same columns, the same `OnQuery` and the same writer. **One announcement,
not two**: a trigger a header draws and an act something else runs are the same list's export, and
announced apart they could name two grids on one surface. So the act appears and vanishes with the
trigger — a grid that is a panel of a bigger page (`Title`) or has nothing to export announces
neither.

**`Run` answers the rows the file carries, or null where no file was written.** A reader sees the
download; a caller away from the screen sees nothing, and "it worked" is the one thing it cannot
be left to assume — a read that does not land leaves no file (below), a kind nobody registered has
no writer, and both are null. The count is taken as the writer pulls the rows past, so it is the
rows of the FILE and never a total something computed for a pager.

**Each option's icon is the writer's own** (`IExportWriter.Icon`), named through its own catalog
entry — `ExportingIcons` (`NSail.Exporting`) — never pasted inline: a writer a host adds brings
its glyph the same way, naming an entry of its own catalog rather than a mapping chrome keeps.

**Whose ROWS a grid shows is a second question the derivation above does not ask, and some grids
answer it NO.** A list offers Exportar when its rows are the business's own records — what a
person takes to a spreadsheet to work with (articles, stock, sales, purchases, prices, patients,
sessions, ledgers, debtors). It does not where the rows are the app's OWN machinery (export
requests, background jobs, sign-ins, notifications, its own logs), or a short vocabulary set up
once (colors, kinds, the few taxes) that nobody works with in a spreadsheet. The derived default
stands either way — a business list must not forget it — so the exclusion is an explicit opt-out,
`Exportable="false"`, never a reason to touch the derivation itself: a grid that opts out still
offers Agregar and still lays out its pager the same way, it only drops the one menu.

**Both kinds of list are read out, and the reader cannot tell them apart anyway.** A grid the
SERVER pages is a list by construction; a grid holding every row in hand is one too — the register
a kit writes when the whole table fits in the client — and "all pages" is every row either way.
What is NOT a list is a collection a **document** holds, whose rows are a field of what the submit
saves, and a grid **typed into**, whose rows are the thing being written. So the offer is derived
from `RowEditor is null` plus either `OnQuery.HasDelegate` or rows in hand that nothing writes:
no document around the grid (`IFormDocument`, cascaded by `NsForm` and nothing else), no `NewRow`
and no row callbacks. **The form is the test, not the paging**: a collection inside a document is
the one thing in the house whose rows are not there to be read.

**A list the client holds is cut into the same pages by the same reader** (`TableExport.Held`):
there is no server to ask, and a second count of where a list ends would be a second place for the
off-by-one to live. The slice is taken when Exportar is pressed, because the `Rows` parameter moves
when the page that owns it renders and a file written out of a moving parameter would be two
filters' rows under one heading.

**And it is taken in the order the reader is looking at it** (`NsTable.HeldRows`): a heading
pressed to order the list orders the file with it. A file in another order than its own list is
the same defect as one three hours off it — what the list promises is all of it *in order*, and
the order is the one on screen ([ui/hosts.md](ui/hosts.md)). A server-paged list has no such order
to carry: its headings are not pressable in the first place.

**The columns are the header's own declarations.** `NsTh` already carries a `For` expression and
already resolves a heading from it; `TableColumns` keeps those in header order and `TableColumn`
turns each into a key, a title, a format and a reader. Nothing about a column is stated twice, and
nothing is discovered by looking at what a row happens to carry — **the member a column NAMED is
read, so a row type's other members are none of an export's business.**

- **The format is derived from that member's type** — a date member is a date, a numeric one a
  number, everything else text. `NsTh Format=` exists for the one thing a CLR type cannot say:
  an amount and a share are both decimals, so **Money and Percent are declared and nothing else
  is.**
- **An enum reads as the word the cell shows.** A file saying "Supplier" where the screen said
  "Proveedor" is the list mistranslated.
- **A boolean reads as Sí or No** (`Common.Yes`/`Common.No`), the same promise one step down:
  `True` under a heading that says Activo is the CLR's word for it and nobody else's.
- **A timestamp the server stamped reads in the reader's own clock.** Every cell in the house
  prints a UTC moment through `ToLocalTime`, and a file three hours off the list it came from is
  nobody's reading. Only a `DateTime` that says it is UTC: any other kind is already local, or is
  a date with no zone to it.
- **A collection reads as its members, comma-separated** (a party's roles): no cell type holds a
  list, and left alone the file would print the type's name.
- **A column with no `For` is not a column** — the action column has nothing to put in a file and
  nothing to key on either ([ui/hosts.md](ui/hosts.md)).
- **The column set is rebuilt on every header render, never accumulated.** A column a list's own
  filter DROPS stops being declared, and a set that only grew would put it in the file with
  nothing under it. The pass boundary is `TableColumns.BeginPass()`, called as the header's render
  tree is built — before the headers in it set their parameters.

**Every page the filter answers is read, in pages of the export's own** (`TableExport.PageSize`,
the largest any message accepts — the round trips are the cost and no page is held). Asked for
through the screen's own `OnQuery` where there is one, so the message, its filter and its
authorization are the list's and an export needs nothing of its own. **Where the list ends is the page in hand being
shorter than the one asked for, never a total**: a total is a figure the server computed for a
pager, and a filter applied between two pages makes it a lie.

**Nothing on screen moves while an export runs.** A page-mode grid renders `_page` and a
scroll-mode one `_scrolled`, and the export writes neither — it reads each page off the `Rows`
parameter, which is also why **one export runs at a time** and the menu says so by being disabled.

**A read that does not land leaves NO file.** Half a list downloaded as a whole one is worse than
the refusal already on screen, so `TableExport` throws `ExportInterrupted` rather than ending the
rows quietly. The answer travels as the `Rows` parameter, which moves when the page that owns it
renders — handling an event callback is what re-renders it. **A reader cannot wait for that**, so
`TableExport` takes a reader delegate and the grid's own half is one line: where the list ends and
what a page that does not answer means are settled with no renderer in the way.

**The FILE is held in memory, never the rows.** A browser is handed a download as one blob
(`nsapp.download`, a `DotNetStreamReference` so the bytes cross once and never as base64), so the
writer's output stays in hand — a megabyte or two for tens of thousands of rows — while the result
set it came from is read a page at a time and dropped.
