// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Exporting;

/// <summary>Which files this install can produce, which is the registered writers and nothing
/// else: chrome that offers an export lists these, so a format nobody registered is never
/// offered rather than offered and refused.
/// <para>They are offered in <see cref="ExportKind"/>'s own order, not in registration order:
/// which format reads as the obvious choice is one decision, and a host that happened to add the
/// spreadsheet second would otherwise reorder every menu in the house.</para></summary>
public sealed class ExportWriters
{
    readonly List<IExportWriter> _writers;

    public ExportWriters(IEnumerable<IExportWriter> writers)
    {
        ArgumentNullException.ThrowIfNull(writers);

        _writers = [.. writers.OrderBy(writer => writer.Kind)];
    }

    public IReadOnlyList<IExportWriter> All => _writers;

    public bool IsEmpty => _writers.Count == 0;

    public IExportWriter? For(ExportKind kind)
    {
        return _writers.FirstOrDefault(writer => writer.Kind == kind);
    }

    /// <summary>The name the file lands under: the book's own name with the writer's extension,
    /// with everything a file name cannot carry replaced. The book names a screen, and a screen's
    /// name is prose — a date range with slashes in it is a path, not a file.</summary>
    public static string FileName(ExportBook book, IExportWriter writer)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(writer);

        var name = new string([.. book.Name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character)]).Trim();

        return $"{(name.Length == 0 ? "export" : name)}.{writer.Extension}";
    }
}
