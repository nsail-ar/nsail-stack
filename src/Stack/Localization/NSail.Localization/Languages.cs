// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;

namespace NSail.Localization;

/// <summary>The answers an install owes about language: which ones it can render at all, which
/// ones it offers whoever has chosen none, which one it renders in, and how a client puts that
/// one on. All of it lives here so the server and the client cannot answer differently — one
/// derivation, asked twice.</summary>
public static class Languages
{
    /// <summary>Every language the install carries: what its string sources overlay, the base
    /// language they are written in, and its configured default.</summary>
    public static IReadOnlyCollection<string> Supported(IEnumerable<IStringSource> sources, LanguageOptions options)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(options);

        return sources
            .SelectMany(source => source.Languages)
            .Append(options.Base)
            .Append(options.Default)
            .Where(language => language is { Length: > 0 })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>What the install serves somebody who has chosen nothing: the languages it
    /// declares an offer of, else its default alone. Deliberately narrower than Supported —
    /// Supported carries the base language whether or not the shop ever meant to serve it, so a
    /// browser resolved against it would be answered in English everywhere.</summary>
    public static IReadOnlyCollection<string> Offered(LanguageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string[] declared =
        [
            .. options.Offered
                .Where(language => language is { Length: > 0 })
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];

        return declared.Length > 0 ? declared : [options.Default];
    }

    /// <summary>The culture a client boots in: the language somebody chose, else the one the
    /// install is already serving them (LanguageSettings.Default — what the request was
    /// localized in, not what configuration says). The visitor's browser is not a parameter: its
    /// vote, where the install offers it one, was cast on the server and arrives inside served,
    /// so it is never counted twice. Null when nothing can be decided — the caller leaves the
    /// ambient culture alone.</summary>
    public static CultureInfo? Negotiate(string? preference, string? served)
    {
        if (preference is { Length: > 0 })
        {
            return CultureInfo.GetCultureInfo(preference);
        }

        return served is { Length: > 0 } ? CultureInfo.GetCultureInfo(served) : null;
    }

    /// <summary>Makes a culture the client's own, for words and numbers at once: the ambient
    /// formatter every render reads its dates and money from, and the language LanguageProvider
    /// seeds itself with. Off the browser this does nothing on purpose — a server's culture
    /// belongs to the request that carries it, and a thread default would outlive it and reach
    /// the next visitor.</summary>
    public static void Adopt(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
