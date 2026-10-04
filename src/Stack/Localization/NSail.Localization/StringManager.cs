// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Metadata;
using NSail.Problems;
using NSail.Text;

namespace NSail.Localization;

/// <summary>Resolves localized strings for the current language (see LanguageProvider) from
/// the shared StringCatalog. A missing key renders as the key itself, so untranslated strings
/// are visible instead of silently guessed.</summary>
public sealed class StringManager(StringCatalog catalog, LanguageProvider language, MetadataProvider metadata)
{
    const string EntityArgument = "entity";

    public string Translate(string key, string? fallback = null)
    {
        return Entries.TryGetValue(key, out var value)
            ? value
            : fallback ?? key;
    }

    /// <summary>Count-aware text: one uses the key itself, any other count the ".Plural"
    /// variant, falling back to the key when the variant is absent.</summary>
    public string Translate(string key, int count)
    {
        if (count != 1 && Entries.TryGetValue(key + ".Plural", out var plural))
        {
            return plural;
        }

        return Translate(key);
    }

    /// <summary>Localized text for an enum value ("{Area}.{Enum}.{Value}"), the value's
    /// name as fallback.</summary>
    public string Translate(Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Translate(metadata.KeyFor(value), value.ToString());
    }

    /// <summary>Localized text for an issue, most specific first: "Problems.{Code}.{Source}"
    /// (the rule or field it is about), then "Problems.{Code}", then the message the sender
    /// already put in it. Named tokens ({max}, {to}) are filled from Arguments, so a
    /// translation may reorder them; the "entity" token is a KEY and resolves to the concept's
    /// label before it is filled.</summary>
    public string Translate(Issue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);

        var text = Resolve(issue) ?? issue.Message;

        return Fill(text, Concept(issue.Arguments));
    }

    /// <summary>Localized title for a problem ("Problems.{Code}.Title"), its own title as
    /// fallback. Issues carry the reason; this only names the failure.</summary>
    public string Translate(Problem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        return Translate($"Problems.{problem.Code}.Title", problem.Title);
    }

    /// <summary>Localized text for a content key with named tokens ({name}, {number}) filled
    /// from the arguments, so a template is a localized string and never rendered text. A
    /// missing key renders as the key itself, the same visible fallback the single-argument
    /// overload gives.</summary>
    public string Translate(string key, IReadOnlyDictionary<string, string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return Fill(Translate(key), arguments);
    }

    public bool TryTranslate(string key, out string text)
    {
        if (Entries.TryGetValue(key, out var value))
        {
            text = value;
            return true;
        }

        text = string.Empty;
        return false;
    }

    IReadOnlyDictionary<string, string> Entries => catalog.Get(language.Current);

    string? Resolve(Issue issue)
    {
        if (!string.IsNullOrWhiteSpace(issue.Source)
            && Entries.TryGetValue($"Problems.{issue.Code}.{issue.Source}", out var scoped))
        {
            return scoped;
        }

        return Entries.TryGetValue($"Problems.{issue.Code}", out var generic) ? generic : null;
    }

    // BusinessProblem hands the entity over as the key MetadataProvider.KeyFor renders, because
    // the type it was derived from lives in an assembly the browser never loads. A key with no
    // string in the catalog falls back to its last segment — the type's own name — so the worst
    // an untranslated concept can read is "Store", never "Products.Store".
    IReadOnlyDictionary<string, string>? Concept(IReadOnlyDictionary<string, string>? arguments)
    {
        if (arguments is null || !arguments.TryGetValue(EntityArgument, out var key))
        {
            return arguments;
        }

        return new Dictionary<string, string>(arguments, StringComparer.Ordinal)
        {
            [EntityArgument] = Translate(key, key[(key.LastIndexOf('.') + 1)..])
        };
    }

    static string Fill(string text, IReadOnlyDictionary<string, string>? arguments)
    {
        return NamedTokens.Fill(text, arguments);
    }
}
