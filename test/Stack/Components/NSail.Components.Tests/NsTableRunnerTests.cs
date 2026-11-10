// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>While the host's Runner is busy answering a row's commit or remove, the controls
/// that would start a second act are disabled — the way NsAction disables itself — so the
/// method never guards more than the control shows, and a hand is never given a press that
/// does nothing.</summary>
public sealed class NsTableRunnerTests : BunitContext, IAsyncLifetime
{
    public NsTableRunnerTests()
    {
        Services.AddMudServices();
        Services.AddScoped<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Actions.Add"] = "Agregar",
            ["Actions.Edit"] = "Editar",
            ["Actions.Delete"] = "Eliminar",
            ["Actions.Confirm"] = "Confirmar",
            ["Actions.Cancel"] = "Cancelar",
            ["Common.LoadMore"] = "Mas",
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static bool IsDisabled(IRenderedComponent<TableRunnerHost> cut, string selector)
    {
        return cut.Find(selector).HasAttribute("disabled");
    }

    async Task<IRenderedComponent<TableRunnerHost>> Loaded()
    {
        var cut = Render<TableRunnerHost>();

        await cut.WaitForAssertionAsync(() => Assert.Contains("Ana", cut.Markup));

        return cut;
    }

    [Fact]
    public async Task AddAndDeleteAreDisabledWhileARemoveRuns()
    {
        var cut = await Loaded();
        var gate = new TaskCompletionSource();

        cut.Instance.Gate = gate;

        Assert.False(IsDisabled(cut, ".ns-row-delete"));
        Assert.False(IsDisabled(cut, ".ns-cells-add"));

        var running = cut.InvokeAsync(() => cut.Find(".ns-row-delete").Click());

        await cut.WaitForAssertionAsync(() =>
        {
            Assert.True(IsDisabled(cut, ".ns-row-delete"));
            Assert.True(IsDisabled(cut, ".ns-cells-add"));
        });

        gate.SetResult();
        await running;

        await cut.WaitForAssertionAsync(() => Assert.False(IsDisabled(cut, ".ns-cells-add")));
    }

    [Fact]
    public async Task ConfirmAndCancelAreDisabledWhileACommitRuns()
    {
        var cut = await Loaded();
        var gate = new TaskCompletionSource();

        await cut.InvokeAsync(() => cut.Find(".ns-row-edit").Click());

        Assert.False(IsDisabled(cut, ".ns-row-commit"));

        cut.Instance.Gate = gate;

        var running = cut.InvokeAsync(() => cut.Find(".ns-row-commit").Click());

        await cut.WaitForAssertionAsync(() =>
        {
            Assert.True(IsDisabled(cut, ".ns-row-commit"));
            Assert.True(IsDisabled(cut, "button[aria-label='Cancelar']"));
        });

        gate.SetResult();
        await running;

        Assert.Equal(1, cut.Instance.Commits);
    }

    [Fact]
    public async Task APageReadTheBusyRunnerRefusedLeavesTheGridAbleToFetchAgain()
    {
        var cut = await Loaded();
        var gate = new TaskCompletionSource();

        cut.Instance.Gate = gate;

        var running = cut.InvokeAsync(() => cut.Find(".ns-row-delete").Click());

        await cut.WaitForAssertionAsync(() => Assert.True(IsDisabled(cut, ".ns-row-delete")));

        // The refusal escapes to the renderer as an unhandled exception, which bUnit raises
        // from the next wait.
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await cut.InvokeAsync(() => cut.Find(".ns-table-more button").Click());
            await cut.WaitForAssertionAsync(() => Assert.Fail("waiting for the renderer's report"), TimeSpan.FromMilliseconds(500));
        });

        gate.SetResult();
        await running;

        Assert.False(IsDisabled(cut, ".ns-table-more button"));
    }
}
