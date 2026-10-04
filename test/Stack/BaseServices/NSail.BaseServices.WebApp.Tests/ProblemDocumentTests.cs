// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace NSail.BaseServices.WebApp.Tests;

/// <summary>The proof of nsail#437: a request that dies before render answers a person with a
/// page and a machine with the same bytes it always did. Both arms hang off <c>Accept</c>
/// alone — nothing reads the path, the caller or a header of our own invention.</summary>
public sealed class ProblemDocumentTests : IAsyncLifetime
{
    // What a browser sends on a document navigation, verbatim: html outranks everything, and
    // json only ever arrives under the */* tail.
    const string Browser = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

    // The bytes the machine contract is frozen at — application/json and not problem+json,
    // which is what WriteAsJsonAsync has always written.
    const string Json = "application/json; charset=utf-8";

    // Written out rather than round-tripped through the Problem that produced it: a machine
    // contract this story froze is only pinned by bytes nobody can regenerate.
    const string Bytes =
        """{"code":"NotFound","title":"Resource not found","issues":[{"code":"NotFound","message":"Resource with id /gone was not found","source":"Resource","arguments":{"entity":"Common.Resource","id":"/gone"}}],"status":404}""";

    ProblemDocumentHost _host = null!;

    public async Task InitializeAsync()
    {
        // The page speaks the app's words, and which words is a culture question: pinning it
        // is what lets the English catalog be asserted verbatim.
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en");

        _host = await ProblemDocumentHost.Start(composesApp: true);
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task ADocumentNavigationGetsThePageAndNotTheJson()
    {
        using var response = await _host.Get(Browser);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("id=\"ns-page-error\"", body, StringComparison.Ordinal);
        Assert.Contains("Something broke.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"code\":\"NotFound\"", body, StringComparison.Ordinal);
    }

    // The face's whole chrome reads --mud-palette-*, and the vendor stylesheet carries no :root
    // block that sets them: nothing but a mounted theme does. Under the layout NsSetup mounts
    // one; this document has no layout, so it mounts its own, and the neutral grey is what it
    // paints because the request died before any brand could be resolved.
    [Fact]
    public async Task ThePageComposesTheThemeItsColoursAreReadFrom()
    {
        using var response = await _host.Get(Browser);

        var body = await response.Content.ReadAsStringAsync();

        // The same grey NsSetupBrandTests pins as NeutralDark, in the form it reaches the
        // browser in — a brand's own colour here would mean the document guessed one.
        Assert.Contains("--mud-palette-primary: rgba(138,135,130,1);", body, StringComparison.Ordinal);
        Assert.Contains("--mud-palette-background:", body, StringComparison.Ordinal);
    }

    // The door back is the app's root, read off the route table rather than written, and the
    // second door is an anchor: rendered without a circuit, an OnClick is a door that does not
    // open, so Reload has to be a real href to the address the reader is already at.
    [Fact]
    public async Task ThePageCarriesBothDoorsAsAnchors()
    {
        using var response = await _host.Get(Browser);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Matches("<a [^>]*href=\"/\"[^>]*ns-page-error-home", body);
        Assert.Matches("<a [^>]*href=\"http://localhost/gone\"[^>]*ns-page-error-reload", body);
    }

    // A handle a report can be traced by, and nothing else: what the thrower said is not on the
    // page, so the reader is asked for a reference rather than for a payload they copied out of
    // a browser window.
    [Fact]
    public async Task ThePageCarriesAHandleAndLeaksNothingElse()
    {
        using var response = await _host.Get(Browser);

        var body = await response.Content.ReadAsStringAsync();
        var reference = Regex.Match(body, "Reference ([^<]+)");

        Assert.True(reference.Success, "The page carries no reference line.");

        // The string is "Reference {handle}", so the line above matches whether or not anything
        // filled the token: what makes a broken fill red is asserting the capture is a handle
        // and not the token itself.
        var handle = reference.Groups[1].Value.Trim();

        Assert.NotEmpty(handle);
        Assert.DoesNotContain("{handle}", body, StringComparison.Ordinal);
        Assert.Matches("^[0-9A-Za-z:.-]+$", handle);
        Assert.DoesNotContain(ProblemDocumentHost.Secret, body, StringComparison.Ordinal);
        Assert.DoesNotContain("KeyNotFoundException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ErrorMiddleware", body, StringComparison.Ordinal);
    }

    // The SDK sets no Accept at all and a fetch or curl asks with */*: neither states a
    // preference, and the default where none is stated is the machine's answer.
    [Theory]
    [InlineData(null)]
    [InlineData("*/*")]
    [InlineData("application/json")]
    public async Task ACallerThatDidNotAskForADocumentGetsTodaysBytes(string? accept)
    {
        using var response = await _host.Get(accept);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(Json, response.Content.Headers.ContentType?.ToString());
        Assert.Equal(Bytes, await response.Content.ReadAsStringAsync());
    }

    // nsail#1837: the request that died DURING a render is the one the error page could not
    // answer. Writing the document re-renders against the same request, and the NavigationManager
    // a page injects is one per request, already initialized by the render that died — so the
    // second one threw "'RemoteNavigationManager' already initialized", escaped the handler and
    // masked the exception it was called to report. Both halves are asserted: the reader gets
    // the face, and nothing of the second render's own failure reaches them.
    [Fact]
    public async Task ARequestThatDiedMidRenderStillGetsThePage()
    {
        using var response = await _host.Get(Browser, ProblemDocumentHost.DeadMidRender);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("id=\"ns-page-error\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("already initialized", body, StringComparison.Ordinal);
        Assert.DoesNotContain(ProblemDocumentHost.Secret, body, StringComparison.Ordinal);
    }

    // And the class, not just the instance: a document render that fails for any reason at all
    // answers the caller with the Problem the handler had already decided, instead of leaving
    // this handler as the request's fault and burying it.
    [Fact]
    public async Task ADocumentThatCannotBeDrawnFallsBackToTheMachinesAnswer()
    {
        await using var unrenderable = await ProblemDocumentHost.Start(composesApp: true, documentFails: true);

        using var response = await unrenderable.Get(Browser);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(Json, response.Content.Headers.ContentType?.ToString());
        Assert.Equal(Bytes, body);
        Assert.DoesNotContain(UnrenderableDocumentProvider.Masking, body, StringComparison.Ordinal);
    }

    // A host that composes no Blazor app registers no provider, the resolve answers null, and
    // even the browser's own Accept lands on the JSON — AC6 holding by construction.
    [Fact]
    public async Task AHostThatComposesNoAppAnswersExactlyAsItDidBefore()
    {
        await using var bare = await ProblemDocumentHost.Start(composesApp: false);

        using var asked = await bare.Get(Browser);

        Assert.Equal(HttpStatusCode.NotFound, asked.StatusCode);
        Assert.Equal(Json, asked.Content.Headers.ContentType?.ToString());
        Assert.Equal(Bytes, await asked.Content.ReadAsStringAsync());
    }

}
