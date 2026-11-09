// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Injection.Annotations;
using NSail.Localization;
using NSail.Messaging;

namespace NSail.Sample.Strings;

[Injectable]
public sealed class StringsHandler(StringCatalog catalog) : IHandler<GetStrings, Dictionary<string, string>>
{
    public async Task<Dictionary<string, string>> Handle(GetStrings message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var strings = await catalog.GetAsync(message.Language);

        return new Dictionary<string, string>(strings);
    }
}
