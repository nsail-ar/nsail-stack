// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSail.Data;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>What the install's record about a tenant does to its requests: a suspended customer
/// is answered the suspended page and reaches nothing else, a pause written ahead of time starts
/// and ends on the clock rather than on a job, and the page says what the row and the install
/// gave it to say.</summary>
public sealed class TenantStateTests
{
    const string Slug = "lumina";

    [Fact]
    public async Task A_tenant_with_no_state_row_is_served()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        using var response = await host.Client.SendAsync(host.Get("/where", Slug));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // A word this Stack does not know is not a suspension: the column is written by a program
    // outside the app, and a typo in that hand must not take a paying customer down.
    [Fact]
    public async Task A_state_this_stack_cannot_read_is_not_a_suspension()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        await State(host, Slug, new TenantState { Status = "paused-ish" });

        using var response = await host.Client.SendAsync(host.Get("/where", Slug));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("es", "Cuenta suspendida")]
    [InlineData("en", "Account suspended")]
    public async Task A_suspended_tenants_browser_gets_the_suspended_page(string language, string title)
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, language: language);

        await State(host, Slug, new TenantState { Status = TenantState.Suspended });

        var document = await Page(host);

        Assert.Contains(title, document, StringComparison.Ordinal);
        Assert.Contains($"<html lang=\"{language}\"", document, StringComparison.Ordinal);

        // The page carries no asset, and that is the design: every asset of this install is an
        // endpoint behind this gate, so a document that linked one would be refused its own
        // stylesheet and arrive bare.
        Assert.DoesNotContain("<link", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", document, StringComparison.OrdinalIgnoreCase);
    }

    // The other caller: a generated client, a fetch, curl. Accept alone tells them apart, and
    // the machine gets the machine's answer — the same negotiation the error handler makes.
    [Fact]
    public async Task A_suspended_tenants_client_gets_the_problem()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        await State(host, Slug, new TenantState { Status = TenantState.Suspended });

        using var response = await host.Client.SendAsync(host.Get("/where", Slug));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Suspended", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // The gate stands where the install that does not exist is refused: ahead of
    // authentication, so a suspended tenant cannot even mint a ticket.
    [Fact]
    public async Task A_suspended_tenant_cannot_sign_in()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        await State(host, Slug, new TenantState { Status = TenantState.Suspended });

        using var response = await host.Client.SendAsync(host.Get("/sign-in?stamp=true", Slug));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task A_suspended_tenant_does_not_suspend_its_neighbour()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        await State(host, Slug, new TenantState { Status = TenantState.Suspended });

        using var refused = await host.Client.SendAsync(host.Get("/where", Slug));
        using var served = await host.Client.SendAsync(host.Get("/where", "aurora"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    // The window, walked across both its edges inside ONE memo window: the row is read from the
    // database on the first request and every answer after that is the same held row judged
    // against the clock again. Which is the whole point — the cut lands to the second even
    // though nothing re-read the row, and no job had to run at the minute it opened.
    [Fact]
    public async Task A_pause_window_opens_and_closes_on_the_clock()
    {
        var clock = new TestClock();

        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, clock: clock);

        await State(
            host,
            Slug,
            new TenantState
            {
                SuspendedFrom = clock.UtcNow.AddSeconds(30),
                SuspendedUntil = clock.UtcNow.AddSeconds(90),
            });

        Assert.Equal(HttpStatusCode.OK, await Status(host));

        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, await Status(host));

        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, await Status(host));

        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(HttpStatusCode.OK, await Status(host));
    }

    [Fact]
    public async Task A_window_with_no_end_does_not_end()
    {
        var clock = new TestClock();

        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, clock: clock);

        await State(host, Slug, new TenantState { SuspendedFrom = clock.UtcNow.AddSeconds(30) });

        Assert.Equal(HttpStatusCode.OK, await Status(host));

        clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, await Status(host));

        clock.Advance(TimeSpan.FromDays(400));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, await Status(host));
    }

    // An end with no beginning is not a window: it is what the switch wears to say when it comes
    // back, and on its own it suspends nobody.
    [Fact]
    public async Task An_end_with_no_beginning_is_not_a_suspension()
    {
        var clock = new TestClock();

        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, clock: clock);

        await State(host, Slug, new TenantState { SuspendedUntil = clock.UtcNow.AddDays(1) });

        Assert.Equal(HttpStatusCode.OK, await Status(host));
    }

    // The switch is open-ended by contract, so a date beside it is a promise to the reader and
    // not a release: an operator who wants the pause to lift itself writes the window instead.
    [Fact]
    public async Task The_switch_is_not_lifted_by_a_window_that_closed()
    {
        var clock = new TestClock();

        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, clock: clock);

        await State(
            host,
            Slug,
            new TenantState
            {
                Status = TenantState.Suspended,
                SuspendedFrom = clock.UtcNow.AddDays(-2),
                SuspendedUntil = clock.UtcNow.AddDays(-1),
            });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, await Status(host));
    }

    // What the memo costs, said out loud: the row is written by a program outside this process,
    // so a suspension does not arrive until the entry it would replace has expired. One read
    // serves every request until then — which is why the second request below is still served.
    [Fact]
    public async Task A_suspension_written_from_outside_arrives_when_the_memo_expires()
    {
        var clock = new TestClock();

        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, clock: clock);

        Assert.Equal(HttpStatusCode.OK, await Status(host));

        await State(host, Slug, new TenantState { Status = TenantState.Suspended });

        Assert.Equal(HttpStatusCode.OK, await Status(host));

        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, await Status(host));
    }

    [Fact]
    public async Task A_reactivated_tenant_is_served_again_when_the_memo_expires()
    {
        var clock = new TestClock();

        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, clock: clock);

        await State(host, Slug, new TenantState { Status = TenantState.Suspended });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, await Status(host));

        await State(host, Slug, new TenantState { Status = TenantState.Active });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, await Status(host));

        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(HttpStatusCode.OK, await Status(host));
    }

    // Work on the service says so and promises a return; everything else gets the generic words,
    // which are the only honest ones for a pause the page cannot explain.
    [Theory]
    [InlineData(TenantState.Reasons.Maintenance, "Volvemos en breve")]
    [InlineData(TenantState.Reasons.Move, "Volvemos en breve")]
    [InlineData(TenantState.Reasons.Payment, "Cuenta suspendida")]
    [InlineData("vacaciones", "Cuenta suspendida")]
    [InlineData(null, "Cuenta suspendida")]
    public async Task The_page_words_itself_by_the_reason(string? reason, string title)
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, language: "es");

        await State(host, Slug, new TenantState { Status = TenantState.Suspended, Reason = reason });

        Assert.Contains(title, await Page(host), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_page_says_when_the_service_comes_back()
    {
        var clock = new TestClock();

        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, language: "es", clock: clock);

        await State(
            host,
            Slug,
            new TenantState
            {
                Reason = TenantState.Reasons.Maintenance,
                SuspendedFrom = clock.UtcNow.AddSeconds(-1),
                SuspendedUntil = new DateTime(2026, 6, 1, 18, 30, 0, DateTimeKind.Utc),
            });

        Assert.Contains("Volvemos el 01/06/2026 18:30 UTC.", await Page(host), StringComparison.Ordinal);
    }

    // A date already behind the reader would be a lie, and the switch does not lift at it, so
    // the line is drawn only while it is still ahead.
    [Fact]
    public async Task The_page_does_not_promise_a_return_that_is_already_past()
    {
        var clock = new TestClock();

        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, language: "es", clock: clock);

        await State(
            host,
            Slug,
            new TenantState
            {
                Status = TenantState.Suspended,
                SuspendedUntil = clock.UtcNow.AddDays(-1),
            });

        Assert.DoesNotContain("Volvemos el", await Page(host), StringComparison.Ordinal);
    }

    // The writer is trusted and the page is still markup: what the row says is shown, and what
    // it could do to the document is not.
    [Fact]
    public async Task The_page_shows_the_rows_own_message_escaped()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, language: "es");

        await State(
            host,
            Slug,
            new TenantState
            {
                Status = TenantState.Suspended,
                Message = "Mudanza de servidor <b>el domingo</b> & el lunes",
            });

        var document = await Page(host);

        Assert.Contains(
            "Mudanza de servidor &lt;b&gt;el domingo&lt;/b&gt; &amp; el lunes",
            document,
            StringComparison.Ordinal);

        Assert.DoesNotContain("<b>", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_page_names_the_contact_the_install_states()
    {
        await using var host = await TenancyHost.Start(
            TenancyMode.SingleDb,
            language: "es",
            contact: "soporte@nsail.ar");

        await State(host, Slug, new TenantState { Status = TenantState.Suspended });

        Assert.Contains("Escribinos a soporte@nsail.ar.", await Page(host), StringComparison.Ordinal);
    }

    // An install with nobody to name says nothing rather than an empty sentence.
    [Fact]
    public async Task The_page_carries_no_contact_line_when_the_install_names_nobody()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb, language: "es");

        await State(host, Slug, new TenantState { Status = TenantState.Suspended });

        Assert.DoesNotContain("Escribinos a", await Page(host), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_plan_a_tenant_is_on_is_read_in_that_tenants_scope()
    {
        await using var host = await TenancyHost.Start(TenancyMode.SingleDb);

        await State(host, Slug, new TenantState { Status = TenantState.Active, Plan = "Pro" });

        await using var scope = host.Services.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>().Enter(Tenant.For(Slug));

        var state = await scope.ServiceProvider.GetRequiredService<TenantStates>().Current();

        Assert.Equal("Pro", state?.Plan);
    }

    static async Task<HttpStatusCode> Status(TenancyHost host)
    {
        using var response = await host.Client.SendAsync(host.Get("/where", Slug));

        return response.StatusCode;
    }

    static async Task<string> Page(TenancyHost host)
    {
        var request = host.Get("/where", Slug);

        request.Headers.Add("Accept", "text/html");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        return await response.Content.ReadAsStringAsync();
    }

    // What whoever operates the fleet does, in the only shape a test can do it: the row is
    // written by the tenant's own key, through no scope of the app's — and replaced whole,
    // because the columns are that writer's contract and it holds all of them.
    static async Task State(TenancyHost host, string tenant, TenantState row)
    {
        await using var scope = host.Services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var key = TenantKey.For(tenant);

        if (await db.Set<TenantState>().FirstOrDefaultAsync(existing => existing.TenantId == key) is { } held)
        {
            db.Remove(held);

            await db.SaveChangesAsync();
        }

        row.TenantId = key;

        db.Add(row);

        await db.SaveChangesAsync();
    }
}
