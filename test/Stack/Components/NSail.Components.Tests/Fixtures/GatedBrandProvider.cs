// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>An <see cref="IBrandProvider"/> a test drives by hand: it answers whatever
/// <see cref="Answer"/> holds — including null, the "not known yet" answer — and only when
/// the test lets it, so the render before the answer arrives can be asserted on.</summary>
public sealed class GatedBrandProvider : IBrandProvider
{
    TaskCompletionSource _gate = new();

    public Brand? Answer { get; set; }

    public int Asked { get; private set; }

    public static GatedBrandProvider Open(Brand? answer)
    {
        var provider = new GatedBrandProvider { Answer = answer };

        provider.Release();

        return provider;
    }

    public void Release()
    {
        _gate.TrySetResult();
    }

    public void Close()
    {
        _gate = new TaskCompletionSource();
    }

    public async Task<Brand?> GetBrand(CancellationToken cancellationToken = default)
    {
        Asked++;

        await _gate.Task;

        return Answer;
    }
}
