// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Reflection;

namespace NSail.Builds;

/// <summary>The build this process is running, read once off the entry assembly. The server
/// that answers and the client the browser booted read the same word because one publish
/// stamps both (<c>deploy/Dockerfile</c> passes <c>InformationalVersion</c> to the whole
/// build), and a tree nothing stamped answers the same default on both sides — which is what
/// keeps a developer's two halves equal.</summary>
public static class BuildVersion
{
    const string Unstamped = "dev";

    public static string Current { get; } = Resolve();

    static string Resolve()
    {
        var informational = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return string.IsNullOrWhiteSpace(informational) ? Unstamped : informational;
    }
}
