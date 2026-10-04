// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

public sealed class FixedThemeProvider : IThemeProvider
{
    readonly ThemeSettings _settings;

    public FixedThemeProvider(bool? darkMode)
    {
        _settings = new ThemeSettings { DarkMode = darkMode };
    }

    public Task<ThemeSettings> GetTheme(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_settings);
    }
}
