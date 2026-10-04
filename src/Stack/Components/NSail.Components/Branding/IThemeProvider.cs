// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>
/// Supplies the active <see cref="ThemeSettings"/>. The default follows the brand;
/// a product with stored settings replaces it with one that reads them.
/// </summary>
public interface IThemeProvider
{
    Task<ThemeSettings> GetTheme(CancellationToken cancellationToken = default);
}

public sealed class NullThemeProvider : IThemeProvider
{
    public Task<ThemeSettings> GetTheme(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ThemeSettings());
    }
}
