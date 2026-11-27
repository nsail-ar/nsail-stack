// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Components;
using NSail.Localization;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Sending;
using NSail.Metadata;
using NSail.Settings;
using NSail.Storage.Settings;
using NSail.Storage.Settings.Models;

namespace NSail.Storage.Tests;

// The screen lists one row per declared store name, in the order the server answers, the
// default under its own words, a kit's name under the words its kit supplies or as declared, and
// each provider in the install's language; a store's form opens in the aside, and its save
// reaches the list underneath through the real Mediator.
public sealed class StorageSettingsPageTests : BunitContext, IAsyncLifetime
{
    readonly StoresServer _server = new();

    public StorageSettingsPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddMessaging();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddStringsFromAssembly(typeof(StorageSettingsPage).Assembly);

        // A kit's strings, composed after the screen's: they word the store "reports" and no other.
        Services.AddStringsFromAssembly(typeof(StorageSettingsPageTests).Assembly);
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<ISender<ListStorageStores, List<StorageStoreRow>>>(_server);
        Services.AddSingleton<ISender<LookupStorageProviders, List<StorageProviderRef>>>(_server);
        Services.AddSingleton<ISender<GetStorageSettings, StoredSettings<StorageSettingsModel>>>(_server);
        Services.AddSingleton<ISender<SaveStorageSettings>>(_server);

        // NsPage resolves its RouteTable from DI, and the one AddRouteTable builds is the entry
        // assembly's — the test host's, which carries none of the screen's routes.
        Services.AddSingleton(new RouteTable(typeof(StorageSettingsPage).Assembly, []));

        // The router's gate asks for the page's own policy, which a rendered page never did.
        AddAuthorization().SetAuthorized("admin").SetPolicies(new AuthorizeAttribute<SaveStorageSettings>().Policy!);
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    [Fact]
    public void DrawsOneRowPerDeclaredStoreName()
    {
        Services.GetRequiredService<LanguageProvider>().Current = "es";

        var app = RenderApp();

        app.WaitForAssertion(() => Assert.Equal(3, app.FindAll("tbody tr").Count));

        var rows = app.FindAll("tbody tr").Select(row => row.TextContent).ToList();

        Assert.Contains("Por defecto", rows[0]);
        Assert.Contains("Carpeta del servidor", rows[0]);
        Assert.Contains("Reportes", rows[1]);
        Assert.Contains("Bucket compatible con S3", rows[1]);
        Assert.Contains("backups", rows[2]);
        Assert.Contains("Carpeta del servidor", rows[2]);
        Assert.Contains("✓", rows[2]);
    }

    [Fact]
    public void ReadsTheProvidersInEnglish()
    {
        var app = RenderApp();

        app.WaitForAssertion(() => Assert.Equal(3, app.FindAll("tbody tr").Count));

        var rows = app.FindAll("tbody tr").Select(row => row.TextContent).ToList();

        Assert.Contains("Default", rows[0]);
        Assert.Contains("Server folder", rows[0]);
        Assert.Contains("Reports", rows[1]);
        Assert.Contains("S3-compatible bucket", rows[1]);
    }

    [Theory]
    [InlineData("es", "Editar Almacén")]
    [InlineData("en", "Edit Store")]
    public void ThePencilNamesItsAct(string language, string name)
    {
        Services.GetRequiredService<LanguageProvider>().Current = language;

        var app = RenderApp();

        app.WaitForAssertion(() => Assert.Equal(3, app.FindAll("tbody tr").Count));

        var pencil = app.FindAll("tbody tr")[0].QuerySelector("a[href*='aside=']")!;

        Assert.Equal(name, pencil.GetAttribute("aria-label"));
        Assert.Equal(name, pencil.GetAttribute("data-ns-whisper"));
    }

    [Fact]
    public void TheFormNamesItsActAndItsStore()
    {
        Services.GetRequiredService<LanguageProvider>().Current = "es";

        var app = RenderApp();

        app.WaitForAssertion(() => Assert.Equal(3, app.FindAll("tbody tr").Count));

        Navigation.NavigateTo("/settings/storage?aside=settings%2Fstorage%2Fdefault");
        app.WaitForAssertion(() => Assert.Contains("Editar Almacén: Por defecto", app.Markup));

        Navigation.NavigateTo("/settings/storage?aside=settings%2Fstorage%2Freports");
        app.WaitForAssertion(() => Assert.Contains("Editar Almacén: Reportes", app.Markup));
    }

    // The options read in the install's language, in its order; what is chosen is still the type.
    [Fact]
    public void TheProviderPickerOffersTheInstallsWords()
    {
        Services.GetRequiredService<LanguageProvider>().Current = "es";

        var app = RenderApp();

        app.WaitForAssertion(() => Assert.Equal(3, app.FindAll("tbody tr").Count));

        Navigation.NavigateTo("/settings/storage?aside=settings%2Fstorage%2Fdefault");
        app.WaitForAssertion(() => Assert.Equal("/data/storage", Field(app, "Contenedor").GetAttribute("value")));

        var picker = app.FindComponent<NsSelect<StorageProviderRef, string?>>();

        Assert.Equal("Carpeta del servidor", picker.Instance.ItemText!(picker.Instance.Items!.Single(provider => provider.Type == "file")));
        Assert.Equal(
            ["Bucket compatible con S3", "Carpeta del servidor", "Repositorio git"],
            picker.Instance.Items!.Select(picker.Instance.ItemText!).ToArray());
    }

    // The whole circuit: the store's form opens in the aside without tearing the list down, its
    // submit publishes, and the list underneath reads again and draws the saved target.
    [Fact]
    public async Task ASaveInTheAsideRedrawsTheListUnderneath()
    {
        var app = RenderApp();

        app.WaitForAssertion(() => Assert.Equal(3, app.FindAll("tbody tr").Count));

        Navigation.NavigateTo("/settings/storage?aside=settings%2Fstorage%2Freports");
        app.WaitForAssertion(() => Assert.Equal("reports", Field(app, "Container").GetAttribute("value")));

        await app.InvokeAsync(() => Field(app, "Container").Input("reports-archive"));
        await app.InvokeAsync(() => app.Find("form").Submit());

        Assert.Contains("reports-archive", app.FindAll("tbody tr")[1].TextContent);
        Assert.Equal(2, _server.Lists);
    }

    // A write-only token never travels back, so the one line that says it is on file must keep
    // showing for a repository; the hint about what to paste is only for an empty box.
    [Theory]
    [InlineData(false, "The access token with permission to push.", "••••••••")]
    [InlineData(true, "••••••••", "The access token with permission to push.")]
    public void ARepositoryStoreSaysWhetherATokenIsOnFile(bool hasSecretKey, string shown, string hidden)
    {
        _server.Stores[1].Provider = "git";
        _server.HasSecretKey = hasSecretKey;

        var app = RenderApp();

        app.WaitForAssertion(() => Assert.Equal(3, app.FindAll("tbody tr").Count));

        Navigation.NavigateTo("/settings/storage?aside=settings%2Fstorage%2Freports");
        app.WaitForAssertion(() => Assert.Contains("Branch", app.FindAll("form label").Select(label => label.TextContent)));

        var form = app.Find("form").InnerHtml;

        Assert.Contains(shown, form);
        Assert.DoesNotContain(hidden, form);
    }

    // «Bucket compatible con S3» does not fit half the aside's line, so the provider's item holds
    // its own width; the rest of the form keeps its own natures, for whichever provider shows
    // them.
    [Theory]
    [InlineData("s3", "Region")]
    [InlineData("git", "Branch")]
    public void TheProviderHoldsItsLongestWordWhileTheOtherFieldsKeepTheirWidths(string provider, string shortField)
    {
        _server.Stores[1].Provider = provider;

        var app = RenderApp();

        app.WaitForAssertion(() => Assert.Equal(3, app.FindAll("tbody tr").Count));

        Navigation.NavigateTo("/settings/storage?aside=settings%2Fstorage%2Freports");
        app.WaitForAssertion(() => Assert.Contains(shortField, app.FindAll("form label").Select(label => label.TextContent)));

        Assert.Equal("ns-grid-item ns-grid-item-wide", Item(app, "Provider"));
        Assert.Equal("ns-grid-item-grow", Item(app, "Container"));
        Assert.Equal("ns-grid-item-grow", Item(app, "Service address"));
        Assert.Equal("ns-grid-item", Item(app, shortField));
    }

    static string Item(IRenderedComponent<NsRouter> app, string label)
    {
        var element = app.FindAll("form label").Single(element => element.TextContent == label).ParentElement;

        while (element is not null && !element.ClassList.Any(name => name.StartsWith("ns-grid-item", StringComparison.Ordinal)))
        {
            element = element.ParentElement;
        }

        return element!.ClassName!;
    }

    static AngleSharp.Dom.IElement Field(IRenderedComponent<NsRouter> app, string label)
    {
        var id = app.FindAll("form label").Single(element => element.TextContent == label).GetAttribute("for");

        return app.Find($"#{id}");
    }

    IRenderedComponent<NsRouter> RenderApp()
    {
        Navigation.NavigateTo("/settings/storage");

        return Render<NsRouter>(p => p
            .Add(x => x.AppAssembly, typeof(StorageSettingsPage).Assembly)
            .Add(x => x.AdditionalAssemblies, [])
            .Add(x => x.DefaultLayout, typeof(StoresLayout)));
    }
}
