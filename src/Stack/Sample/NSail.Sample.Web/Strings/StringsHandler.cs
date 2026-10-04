// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Injection.Annotations;
using NSail.Localization;
using NSail.Messaging;

namespace NSail.Sample.Strings;

[Injectable]
public sealed class StringsHandler : IHandler<GetStrings, Dictionary<string, string>>
{
    readonly StringCatalog _catalog;
    readonly IEnumerable<IStringSource> _sources;
    readonly LanguageOptions _options;

    public StringsHandler(StringCatalog catalog, IEnumerable<IStringSource> sources, LanguageOptions options)
    {
        _catalog = catalog;
        _sources = sources;
        _options = options;
    }

    public Task<Dictionary<string, string>> Handle(GetStrings message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Only a language the install carries is ever loaded: the catalog keeps one dictionary per
        // language it is asked for, so an arbitrary route value would grow it without bound.
        var supported = Languages.Supported(_sources, _options);
        var language = supported.FirstOrDefault(l => string.Equals(l, message.Language, StringComparison.OrdinalIgnoreCase))
            ?? _options.Default;

        return Task.FromResult(new Dictionary<string, string>(_catalog.Get(language)));
    }
}
