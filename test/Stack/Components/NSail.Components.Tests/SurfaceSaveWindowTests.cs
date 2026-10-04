// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The save window's own contract, driven at the seam: a window belongs to the CALL that
/// opened it and answers only for what happened inside it. The orderings here are the ones no pair
/// of forms can drive from a bUnit test — a form's Runner refuses a re-entrant run before its
/// handler starts, and two genuinely in-flight handlers need a gate — while the reachable halves
/// are pinned through the real form in UnsavedChangesGuardTests and NsFormCloseOnSubmitTests.</summary>
public sealed class SurfaceSaveWindowTests : BunitContext, IAsyncLifetime
{
    public SurfaceSaveWindowTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
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

    SurfaceContext Surface()
    {
        var host = Render<SurfaceFormHost>(p => p
            .Add(x => x.RouteTable, new RouteTable(typeof(SurfaceSaveWindowTests).Assembly, Array.Empty<System.Reflection.Assembly>()))
            .Add(x => x.Model, new SubmitTrackingModel()));

        return host.Instance.Surface!;
    }

    /// <summary>Two submits on one document — a second press of a button nothing disabled — and the
    /// second one closing first: it takes its own window and leaves the first's standing, so the
    /// echo the first save files afterwards is still held rather than arming the guard against the
    /// navigation that save is about to make.</summary>
    [Fact]
    public void ASecondSaveOnTheSameDocument_ClosesOnlyItsOwnWindow()
    {
        var surface = Surface();
        var document = new Document();
        var editor = new object();

        var first = surface.BeginSave(document);
        var second = surface.BeginSave(document);

        Assert.Empty(surface.EndSave(second));

        surface.SetDirty(editor);

        Assert.False(surface.HasChanges);
        Assert.Contains(editor, surface.EndSave(first));
    }

    /// <summary>And a window closes once: the same call's second EndSave answers with nothing
    /// rather than draining whatever stands open.</summary>
    [Fact]
    public void AWindowClosedTwice_ClosesNothingTheSecondTime()
    {
        var surface = Surface();
        var document = new Document();
        var editor = new object();

        var window = surface.BeginSave(document);

        surface.SetDirty(editor);
        surface.EndSave(window);

        var standing = surface.BeginSave(document);

        surface.SetDirty(editor);

        Assert.Empty(surface.EndSave(window));
        Assert.Contains(editor, surface.EndSave(standing));
    }

    /// <summary>A refusal hands its report back while another document's save is still in flight.
    /// That save is about to navigate out of its own handler, so the report waits in its window
    /// instead of arming the guard against that departure — and it is not that save's to drop: it
    /// reaches the surface at its close, whether the save took or not.</summary>
    [Fact]
    public void ARefusalInsideAnotherDocumentsWindow_ReachesTheSurfaceAtThatWindowsClose()
    {
        var surface = Surface();
        var editor = new object();

        var refusing = surface.BeginSave(new Document());
        var saving = surface.BeginSave(new Document());

        surface.SetDirty(editor);

        surface.PutBack(surface.EndSave(refusing));

        Assert.False(surface.HasChanges);

        surface.EndSave(saving);

        Assert.True(surface.HasChanges);
    }

    /// <summary>What the spend took is owed back the same way: a refused submit's put-back is not
    /// swallowed for good by a save standing open over it.</summary>
    [Fact]
    public void ASpentReportPutBackInsideAnotherDocumentsWindow_ReachesTheSurface()
    {
        var surface = Surface();
        var document = new Document();
        var editor = new object();

        surface.SetDirty(editor);

        var saving = surface.BeginSave(new Document());
        var spent = surface.ClearChanges(document);

        surface.PutBack(spent);

        Assert.False(surface.HasChanges);

        surface.EndSave(saving);

        Assert.True(surface.HasChanges);
    }

    /// <summary>A source that spends its own report in there — an editor's ClearDirty, the Retire
    /// an unmount goes through — is owed nothing back by any window holding it.</summary>
    [Fact]
    public void ASourceThatSpendsItsOwnReportInsideAWindow_IsOwedNothing()
    {
        var surface = Surface();
        var editor = new object();

        var refusing = surface.BeginSave(new Document());
        var saving = surface.BeginSave(new Document());

        surface.SetDirty(editor);
        surface.SetUnchanged(editor);

        surface.PutBack(surface.EndSave(refusing));
        surface.EndSave(saving);

        Assert.False(surface.HasChanges);
    }

    // A form, as the surface knows one: a document of its own, so the spend and the window of the
    // document beside it never reach it (IFormDocument).
    sealed class Document : IFormDocument
    {
    }
}
