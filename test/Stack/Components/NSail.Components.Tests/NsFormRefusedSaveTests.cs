// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Problems;

namespace NSail.Components.Tests;

/// <summary>Issue #47's second leg: a failed save is unmissable. The refusal it is actually
/// about is the security one, and that shape had never been pinned — every case in
/// NsFormProblemDisplayTests names a field, a rule or nothing, while the gate names the
/// MESSAGE ("SaveGoogleSettings"), and the 401 it now answers an expired session names nothing
/// at all. Both go through TryCreateFieldIdentifier, which is exactly where a source that looks
/// like a member has swallowed a refusal before (6062c3fb).
///
/// So: the reason is on screen, the form still holds what the user typed, and an overlay does
/// not close over it. A save that failed must never look like a save that took.</summary>
public sealed class NsFormRefusedSaveTests : BunitContext, IAsyncLifetime
{
    public NsFormRefusedSaveTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    static string FootAlert<TComponent>(IRenderedComponent<TComponent> cut) where TComponent : class, IComponent
    {
        var feet = cut.FindAll(".ns-form-problem");

        return feet.Count == 0 ? string.Empty : feet[0].TextContent;
    }

    /// <summary>The user's own typing, put in the way a browser puts it — through the input, so
    /// the model holds it because the field bound it and not because the test assigned it.</summary>
    static async Task<IRenderedComponent<FormProblemHost>> Filled(BunitContext context, Problem problem)
    {
        var cut = context.Render<FormProblemHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, problem));

        var inputs = cut.FindAll("input");

        await cut.InvokeAsync(() => inputs[0].Input("Óptica Lúmina"));
        await cut.InvokeAsync(() => inputs[1].Input("lumina"));

        return cut;
    }

    static string[] Values<TComponent>(IRenderedComponent<TComponent> cut) where TComponent : class, IComponent
    {
        return cut.FindComponents<MudTextField<string>>()
            .Select(field => field.Instance.Value ?? string.Empty)
            .ToArray();
    }

    /// <summary>The gate names the message it refused, and a message name is not a member of the
    /// model — so it falls to the form's own line rather than under an input nobody is looking
    /// at. The text is the sender's, untranslated here because the catalog is empty: the Stack
    /// invents no wording for a refusal.</summary>
    [Fact]
    public async Task AForbiddenSave_DrawsAtFormLevel()
    {
        var cut = await Filled(this, SecurityProblem.Forbidden("SaveGoogleSettings"));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Contains("SaveGoogleSettings", FootAlert(cut), StringComparison.Ordinal);
    }

    /// <summary>The refusal an expired session now meets. Its issue carries no source at all,
    /// which is the branch that skips TryCreateFieldIdentifier entirely — and it still has to
    /// say something.</summary>
    [Fact]
    public async Task AnUnauthorizedSave_DrawsAtFormLevel()
    {
        var cut = await Filled(this, SecurityProblem.Unauthorized());

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.NotEqual(string.Empty, FootAlert(cut));
    }

    /// <summary>The data-loss half. Leonardo's Google configuration went twice: the save was
    /// refused, and what mattered was that the thirty seconds of typing were still there to
    /// retry with. A form that reset itself on refusal would lose the values the server never
    /// took.</summary>
    [Fact]
    public async Task ARefusedSave_KeepsWhatTheUserTyped()
    {
        var cut = await Filled(this, SecurityProblem.Forbidden("SaveGoogleSettings"));

        Assert.Equal(["Óptica Lúmina", "lumina"], Values(cut));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.Equal(["Óptica Lúmina", "lumina"], Values(cut));
    }

    /// <summary>Handled, so it never also becomes a toast the user has to connect back to the
    /// form by themselves — the vanishing report intentional-ui.md killed.</summary>
    [Fact]
    public async Task ARefusedSave_IsReportedAsHandled()
    {
        ProblemEventArgs? seen = null;

        var cut = Render<FormProblemHost>(p => p
            .Add(x => x.Model, new FormProblemModel())
            .Add(x => x.Problem, SecurityProblem.Forbidden("SaveGoogleSettings"))
            .Add(x => x.Reported, args => seen = args));

        await cut.InvokeAsync(() => cut.Find("form").Submit());

        Assert.NotNull(seen);
        Assert.True(seen!.Handled);
    }

    /// <summary>An overlay closing is how a save announces it took (NsFormCloseOnSubmitTests).
    /// A security refusal must not earn that announcement — the aside stays, holding both the
    /// message and the values.</summary>
    [Fact]
    public async Task ARefusedSaveInAnAside_KeepsTheAsideOpen()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/settings/google?aside=settings%2Fgoogle%2Fedit");

        var host = Render<SurfaceFormHost>(p => p
            .Add(x => x.RouteTable, new RouteTable(typeof(NsFormRefusedSaveTests).Assembly, []))
            .Add(x => x.Model, new SubmitTrackingModel())
            .Add(x => x.Name, Surfaces.Aside)
            .Add(x => x.Problem, SecurityProblem.Forbidden("SaveGoogleSettings")));

        await host.InvokeAsync(() => host.Find("form").Submit());

        Assert.Contains("aside=", navigation.Uri, StringComparison.Ordinal);
        Assert.Contains("SaveGoogleSettings", host.Find(".ns-form-problem").TextContent, StringComparison.Ordinal);
    }
}
