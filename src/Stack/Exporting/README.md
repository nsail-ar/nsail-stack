# Exporting

A list read out as a file. The capability is one seam in two halves: a format-neutral table that
anything can build, and a writer that turns one into a file. Neither half knows the other, so a new
format is a new project and not a change to the grid that offers the export.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Exporting` | The table model, `IExportWriter`, the `ExportWriters` registry and the CSV writer; no third-party dependency | `NSail.Components` and `NSail.Components.Mud` (the grid offers the export); the host or client that composes it |
| `NSail.Exporting.Excel` | The `.xlsx` writer, on MiniExcel | The client that wants spreadsheets |

## Use it

```xml
<PackageReference Include="NSail.Exporting" Version="$(NSailStackVersion)" />
<PackageReference Include="NSail.Exporting.Excel" Version="$(NSailStackVersion)" />
```

```csharp
services.AddExporting();       // table model, registry, CSV writer
services.AddExcelExport();     // adds the .xlsx writer; optional
```

What an install can produce is what it registered: the grid lists the registered writers and
never offers a format nobody registered. A list grid whose rows are read offers the export with
no per-screen code; a screen opts out with `Exportable="false"`.

To write a file from your own code, build a book and hand it to a writer:

```csharp
var book = ExportBook.Of("Contacts", new ExportTable
{
    Columns =
    [
        new ExportColumn { Key = "name", Title = "Name" },
        new ExportColumn { Key = "balance", Title = "Balance", Format = ExportFormat.Money }
    ],
    Rows = RowsAsync()    // IAsyncEnumerable<ExportRow>, enumerated once
});

var writer = writers.For(ExportKind.Csv)!;

await writer.WriteAsync(book, stream, CultureInfo.CurrentCulture, cancellationToken);
var name = ExportWriters.FileName(book, writer);    // "Contacts.csv"
```

## Configuration

There are no configuration keys. The registration is the configuration.

| Type | Role |
|---|---|
| `ExportBook` | `Name` (no extension) and `Pages`; `ExportBook.Of(name, table)` builds the one-page, one-table book. |
| `ExportPage` | A named sheet holding `Tables`; `ExportPage.Of`. |
| `ExportTable` | Optional `Name`, `Columns`, and `Rows` as `IAsyncEnumerable<ExportRow>`, enumerated once. |
| `ExportColumn` | `Key` (identity within the table), `Title`, `Format`. The format lives on the column and nowhere else. |
| `ExportRow` | `Values` positionally against the columns, as values and not printed text; `ExportRow.Of(...)`. |
| `ExportFormat` | `Text`, `Number`, `Money`, `Date`, `Percent`. |
| `ExportKind` | `Excel`, `Csv`, `Pdf`. Only `Excel` and `Csv` have a writer here. |
| `IExportWriter` | `Kind`, `Icon`, `Extension`, `ContentType`, `WriteAsync(book, destination, culture, ct)`. The culture is passed in, never read from the thread. |
| `ExportWriters` | The registered writers in `ExportKind` order: `All`, `IsEmpty`, `For(kind)`, and `FileName(book, writer)`, which replaces characters a file name cannot carry. |

The two writers differ on purpose:

| | `.xlsx` | `.csv` |
|---|---|---|
| Cells | Typed values with a per-column number format (`#,##0.####`, `#,##0.00`, `0.00%`, the culture's short date) | Text in the culture's separators |
| Pages | One sheet per table (names cut to 31 characters), header row frozen, auto-filter on | Tables flattened in order with a blank line between them |
| Encoding | OOXML | UTF-8 with a byte-order mark |
| Separator | n/a | `;` where the culture's decimal mark is a comma, `,` otherwise; never configured |

A new format is an `IExportWriter` registered as a singleton, with its own entry in an icon catalog
for `Icon` (`ExportingIcons` holds the two built-in ones).

## How it fits

- [Components](../Components/README.md): `NsTable` and `NsTh` declare the columns, page through the
  list's own `OnQuery` to read every row, and draw the export trigger; the formats of a column
  derive from the member's type.
- [Types](../Types/README.md): `Glyph` for writer icons.
- [Localization](../Localization/README.md): the culture the writer is handed.

## Going deeper

- Doctrine: [exporting.md](../../../plugins/nsail-stack/doctrine/exporting.md)
- Sample: [NSail.Sample.Wasm `Screens.cs`](../Sample/NSail.Sample.Wasm/Screens.cs) registers both writers
- Tests: [test/Stack/Exporting](../../../test/Stack/Exporting)
