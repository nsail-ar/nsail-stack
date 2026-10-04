// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Builds;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Messaging.Runtime.Publishing;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>nsail#1480: the chrome above every page is what opens the server's push, in the
/// scope the screens subscribe in, and only for a signed-in session — the hub refuses anybody
/// else, and a tab that signs out must stop hearing the tenant it left.</summary>
public sealed class NsSetupPushTests : BunitContext, IAsyncLifetime
{
    readonly RecordingPushFeed _push = new();
    readonly BunitAuthorizationContext _authorization;

    public NsSetupPushTests()
    {
        Services.AddMudServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton<Mediator>(new CapturingMediator());
        Services.AddSingleton<MessageContextAccessor>();
        Services.AddSingleton<IBrandProvider>(new StaticBrandProvider(new Brand()));
        Services.AddSingleton<IThemeProvider>(new NullThemeProvider());
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton(new LanguageProvider { Current = "en" });
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton<DialogManager>(new CountingDialogManager());
        Services.AddSingleton<ServerBuild>();
        Services.AddSingleton<PushFeed>(_push);

        _authorization = AddAuthorization();

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudPopoverProvider, which NsSetup mounts, holds an async-only vendor service: tearing
    // down through IAsyncLifetime is what keeps BunitContext off its synchronous Dispose.
    async Task IAsyncLifetime.InitializeAsync()
    {
        Services.AddSingleton((await Handoff.Empty()).State);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    [Fact]
    public void ASignedInSessionOpensThePush()
    {
        _authorization.SetAuthorized("vendedor");

        var cut = Render<NsSetup>();

        cut.WaitForAssertion(() => Assert.True(_push.IsOpen));
    }

    [Fact]
    public void AnAnonymousVisitorNeverOpensIt()
    {
        _authorization.SetNotAuthorized();

        var cut = Render<NsSetup>();

        cut.WaitForAssertion(() => Assert.True(_push.Closes > 0));
        Assert.Equal(0, _push.Opens);
    }

    [Fact]
    public void SigningOutClosesIt()
    {
        _authorization.SetAuthorized("vendedor");

        var cut = Render<NsSetup>();

        cut.WaitForAssertion(() => Assert.True(_push.IsOpen));

        _authorization.SetNotAuthorized();

        cut.WaitForAssertion(() => Assert.False(_push.IsOpen));
    }

    sealed class RecordingPushFeed : PushFeed
    {
        public int Opens { get; private set; }

        public int Closes { get; private set; }

        public bool IsOpen { get; private set; }

        public override void Open()
        {
            Opens++;
            IsOpen = true;
        }

        public override Task Close()
        {
            Closes++;
            IsOpen = false;

            return Task.CompletedTask;
        }
    }
}
