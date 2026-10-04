// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Http;

/// <summary>
/// Fills the <see cref="UrlTokens"/> in a URL template. A template with no token resolves to itself.
/// </summary>
public interface IUrlResolver
{
    string Resolve(string urlTemplate);
}
