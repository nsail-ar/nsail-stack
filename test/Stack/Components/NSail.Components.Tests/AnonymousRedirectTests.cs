// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Web;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NSail.Components.Tests.Fixtures;
using NSail.Localization;
using NSail.Metadata;
using NSail.Security;

namespace NSail.Components.Tests;

/// <summary>nsail#469: signing out froze the whole browser tab, not just the app. The flip to
/// anonymous is what every sign-out and every expired credential does, and NsNotAuthorized
/// answered it by navigating from OnParametersSetAsync — on EVERY parameter set. Its own
/// redirect re-rendered the tree above it, the next pass escaped the address the first pass had
/// just written, and the URL roughly doubled per round: /probe/sign-in?returnUrl=probe%2Fsecure
/// then ?returnUrl=probe%2Fsign-in%3FreturnUrl%3Dprobe%252Fsecure, and so on until Chrome
/// stopped answering for the tab. The tree here is the real one — CascadingAuthenticationState
/// over NsRouter over a layout — because the re-entrancy lives in the ancestors, not in the
/// component.</summary>
public sealed class AnonymousRedirectTests : BunitContext
{
    readonly ProbeAuthenticationStateProvider _authentication = new();

    public AnonymousRedirectTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddComponentServices();
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();

        // bUnit's own authorization doubles answer from a context the test sets, not from an
        // AuthenticationStateProvider, so a flip never reaches AuthorizeRouteView — and the flip
        // is the whole subject here. Its placeholders are registered before this constructor and
        // AddAuthorizationCore only ever TryAdds, so they are removed rather than overridden.
        Services.RemoveAll<IAuthorizationService>();
        Services.RemoveAll<IAuthorizationPolicyProvider>();
        Services.RemoveAll<AuthenticationStateProvider>();
        Services.AddAuthorizationCore();
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Services.AddSingleton<AuthenticationStateProvider>(_authentication);
    }

    BunitNavigationManager Navigation
    {
        get { return (BunitNavigationManager)Services.GetRequiredService<NavigationManager>(); }
    }

    // A replace pops the entry it lands on rather than adding beside it (nsail#583), so the
    // setup navigation is not reliably the stack's last entry any more — a redirect that
    // replaces consumes it outright. What the moves the flip caused actually are is whatever
    // the stack holds now that was not there right after setup, read back in the order they
    // happened; NavigationHistory's structural equality is what makes the difference honest.
    IReadOnlyList<NavigationHistory> _historyAtSetup = [];

    IReadOnlyList<string> MovesAfterSetup()
    {
        return Navigation.History.Except(_historyAtSetup).Select(entry => entry.Uri).Reverse().ToList();
    }

    IRenderedComponent<ProbeAppHost> RenderApp(string uri)
    {
        Navigation.NavigateTo(uri);
        _historyAtSetup = [.. Navigation.History];

        return Render<ProbeAppHost>();
    }

    /// <summary>The claim's own reproduction. One move, and the address it carries is the page
    /// the visitor was actually on — a second move is the loop, whatever it lands on.</summary>
    [Fact]
    public void TheFlipToAnonymous_RedirectsToSignInExactlyOnce()
    {
        var app = RenderApp("/probe/secure");

        Assert.NotEmpty(app.FindAll("p.probe-secure"));

        _authentication.SignOut();

        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("p.probe-sign-in")));

        Assert.Equal([$"/probe/sign-in?{SignInRoutes.ReturnUrlParameter}=probe%2Fsecure"], MovesAfterSetup());
    }

    /// <summary>nsail#514: the bounce mints the query key, the sign-in page and every provider
    /// button resolve SignInRoutes.ReturnUrlParameter to read it. Two spellings drop the
    /// destination without a word — the visitor signs in and lands on the app root — so the key
    /// is asked for by the shared name and the value is the address the visitor was on.</summary>
    [Fact]
    public void TheBounce_CarriesTheOriginUnderTheSharedReturnUrlName()
    {
        var app = RenderApp("/probe/secure");

        _authentication.SignOut();

        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("p.probe-sign-in")));

        var query = HttpUtility.ParseQueryString(Navigation.ToAbsoluteUri(Assert.Single(MovesAfterSetup())).Query);

        Assert.Equal(SignInRoutes.ReturnUrlParameter, Assert.Single(query.AllKeys));
        Assert.Equal("probe/secure", query[SignInRoutes.ReturnUrlParameter]);
    }

    /// <summary>The doubling, pinned on its own. A return address carrying an already-escaped
    /// address is what turns a redirect loop into a dead tab, so no move may ever escape the
    /// escape — whatever else changes about how often the redirect fires.</summary>
    [Fact]
    public void NoRedirect_EverCarriesAnAddressThatAlreadyCarriesOne()
    {
        var app = RenderApp("/probe/secure");

        _authentication.SignOut();

        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("p.probe-sign-in")));

        Assert.All(
            MovesAfterSetup(),
            move => Assert.DoesNotContain($"{SignInRoutes.ReturnUrlParameter}%3D", move, StringComparison.Ordinal));
    }

    /// <summary>nsail#1837: the flip was not an expiry, it was a credential the server refused —
    /// a party merged away, a user disabled or deleted — and the state it left says so. The bounce
    /// is the only hand that can pass that on: in a WebAssembly app the refusal arrived as a 401
    /// on a send and the ticket went out with it, so the door has no cookie left to ask about and
    /// would otherwise look like it simply forgot the person. Both parameters ride, because the
    /// address they were on is still worth coming back to.</summary>
    [Fact]
    public void AFlipFromARefusedCredential_TellsTheDoorWhy()
    {
        var app = RenderApp("/probe/secure");

        _authentication.CredentialRefused();

        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("p.probe-sign-in")));

        var query = HttpUtility.ParseQueryString(Navigation.ToAbsoluteUri(Assert.Single(MovesAfterSetup())).Query);

        Assert.Equal("1", query[SignInRoutes.StaleParameter]);
        Assert.Equal("probe/secure", query[SignInRoutes.ReturnUrlParameter]);
    }

    /// <summary>And an ordinary flip says nothing: a sign-out is not a refusal, and a door that
    /// told everybody their session was no longer valid would be saying it to people who had just
    /// ended one on purpose.</summary>
    [Fact]
    public void AnOrdinaryFlip_SaysNothingAboutARefusal()
    {
        var app = RenderApp("/probe/secure");

        _authentication.SignOut();

        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("p.probe-sign-in")));

        Assert.DoesNotContain(
            SignInRoutes.StaleParameter,
            Assert.Single(MovesAfterSetup()),
            StringComparison.Ordinal);
    }

    /// <summary>The other half of the guard, asked of the component's own contract because no
    /// correctly-wired app can mount it there: standing on sign-in, the return address is not
    /// sign-in. That address is the seed the doubling grows from — a return address naming the page
    /// it is a parameter of — and it is a round trip to nowhere on its own merits.</summary>
    [Fact]
    public void TheReturnAddress_IsNeverTheSignInPageItself()
    {
        _authentication.SignOut();

        Navigation.NavigateTo("/probe/sign-in");

        Render<NsNotAuthorized>(p => p
            .AddCascadingValue(_authentication.GetAuthenticationStateAsync())
            .AddCascadingValue(new RouteTable(typeof(ProbePage).Assembly, []))
            .Add(x => x.SignInPage, typeof(ProbeSignInPage)));

        Assert.Equal(["/probe/sign-in"], MovesAfterSetup());
    }

    /// <summary>A visitor bounced off the app root — where sign-out lands — goes to a bare
    /// sign-in: the root is where a sign-in with no return address lands anyway.</summary>
    [Fact]
    public void TheAppRoot_IsNotCarriedAsAReturnAddress()
    {
        _authentication.SignOut();

        Navigation.NavigateTo("/");

        Render<NsNotAuthorized>(p => p
            .AddCascadingValue(_authentication.GetAuthenticationStateAsync())
            .AddCascadingValue(new RouteTable(typeof(ProbePage).Assembly, []))
            .Add(x => x.SignInPage, typeof(ProbeSignInPage)));

        Assert.Equal(["/probe/sign-in"], MovesAfterSetup());
    }

    /// <summary>The guard on the fix: a signed-in visitor who simply lacks the role still gets
    /// the refusal drawn, never a redirect — that path was already correct and stays correct.</summary>
    [Fact]
    public void ASignedInVisitorLackingTheRole_IsRefusedRatherThanRedirected()
    {
        var app = RenderApp("/probe/admin");

        Assert.NotEmpty(app.FindAll("p[role=alert]"));
        Assert.Empty(MovesAfterSetup());
    }

    /// <summary>nsail#1693: the refusal was a literal English sentence with no localization key,
    /// so a Spanish visitor read English on every gated screen of both products. It goes through
    /// the same catalog every other Stack string does.</summary>
    [Fact]
    public void ASignedInVisitorLackingTheRole_ReadsTheLocalizedRefusal()
    {
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.NotAuthorized"] = "translated refusal"
        })]));

        var app = RenderApp("/probe/admin");

        Assert.Equal("translated refusal", app.Find("p[role=alert]").TextContent);
    }

    /// <summary>nsail#583: the protected page is not a place the visitor chose to be, it is a
    /// gate correcting the address — so it must not survive as a browser-history entry. A pushed
    /// entry does: "back" from sign-in would re-request the protected page, the gate would fire
    /// again, and the visitor is trapped between the two. Only a replaced entry answers the
    /// claim.</summary>
    [Fact]
    public void TheRedirectToSignIn_ReplacesTheProtectedPageInHistoryRatherThanPushingOntoIt()
    {
        var app = RenderApp("/probe/secure");

        _authentication.SignOut();

        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("p.probe-sign-in")));

        var redirect = Assert.Single(Navigation.History.Except(_historyAtSetup));

        Assert.True(redirect.Options.ReplaceHistoryEntry);
    }
}
