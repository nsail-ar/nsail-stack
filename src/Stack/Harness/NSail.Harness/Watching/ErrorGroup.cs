// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Security.Cryptography;
using System.Text;

namespace NSail.Harness.Watching;

// The error lines of one bug: the same service, logger, message template and exception type, in
// whatever cell. Its fingerprint is what a story filed for it carries, so the next pass finds it.
public sealed record ErrorGroup(
    string Fingerprint,
    string Service,
    string Source,
    string Template,
    string? ExceptionType,
    int Count,
    IReadOnlyList<string> Cells,
    DateTimeOffset First,
    DateTimeOffset Last,
    LoggedError Sample)
{
    public static IReadOnlyList<ErrorGroup> Of(IEnumerable<LoggedError> errors)
    {
        return errors
            .GroupBy(e => FingerprintOf(e.Service, e.Source, e.Template, e.ExceptionType))
            .Select(g =>
            {
                var newest = g.OrderByDescending(e => e.At).First();
                return new ErrorGroup(g.Key, newest.Service, newest.Source, newest.Template, newest.ExceptionType, g.Count(),
                    g.Select(e => e.Cell).Where(c => c.Length > 0).Distinct().Order(StringComparer.Ordinal).ToList(),
                    g.Min(e => e.At), g.Max(e => e.At), newest);
            })
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Fingerprint, StringComparer.Ordinal)
            .ToList();
    }

    public static string FingerprintOf(string service, string source, string template, string? exceptionType)
    {
        var key = string.Join('\u001f', service.ToLowerInvariant(), source, template, exceptionType ?? "");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..12];
    }

    // Production is every cell whose name ends in none of the suffixes that are not (`optical-test`
    // is test); a name with no ring suffix at all (a machine's) is not a product cell either.
    public static bool IsProd(string cell, IReadOnlyCollection<string> notProd)
    {
        var dash = cell.LastIndexOf('-');
        return dash > 0 && !notProd.Contains(cell[(dash + 1)..], StringComparer.OrdinalIgnoreCase);
    }
}
