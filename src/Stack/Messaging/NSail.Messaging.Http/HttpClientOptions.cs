// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Http;

public sealed class HttpClientOptions
{
    public string? BaseUrl { get; init; }

    public int? TimeoutSeconds { get; init; }

    public Dictionary<string, string>? DefaultHeaders { get; init; }
}