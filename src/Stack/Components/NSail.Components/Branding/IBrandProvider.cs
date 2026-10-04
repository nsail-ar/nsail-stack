// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>
/// Supplies the active <see cref="Brand"/>. The default is a static, hardcoded
/// brand; a provider that resolves per organization/tenant answers null until it
/// knows which organization it is resolving for.
/// </summary>
public interface IBrandProvider
{
    /// <summary>The active brand, or <c>null</c> for "not known yet" — asked before the
    /// session carries the organization whose brand it is. A provider must never answer a
    /// default <see cref="Brand"/> to mean this: the caller cannot tell that apart from a
    /// real answer, so it paints it, and the real one arriving after is the flash.</summary>
    Task<Brand?> GetBrand(CancellationToken cancellationToken = default);
}

public sealed class StaticBrandProvider : IBrandProvider
{
    readonly Brand _brand;

    public StaticBrandProvider(Brand brand)
    {
        _brand = brand;
    }

    public Task<Brand?> GetBrand(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Brand?>(_brand);
    }
}
