// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NSail.Harness.Api;
using NSail.Messaging;
using NSail.Messaging.WebApi;

namespace NSail.Harness.Tests;

public class ControlGateTests
{
    static HarnessOptions Options(string? control = "control-secret")
    {
        return new HarnessOptions { Control = new ControlOptions { Token = control, LeaseToken = "lease-secret" } };
    }

    static Endpoint Message(Type message)
    {
        return new Endpoint(null, new EndpointMetadataCollection(new MessageEndpointMetadata(message)), message.Name);
    }

    static Endpoint Page(bool controlOnly)
    {
        return new Endpoint(null, new EndpointMetadataCollection(controlOnly ? [new ControlOnlyAttribute()] : []), "page");
    }

    static async Task<int> Call(HarnessOptions o, Endpoint endpoint, string? token = null, string host = "127.0.0.1:6190", string method = "POST", string query = "", string? cookie = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Host = new HostString(host);
        context.Request.QueryString = new QueryString(query);
        context.SetEndpoint(endpoint);
        if (token is not null) context.Request.Headers["X-Harness-Token"] = token;
        if (cookie is not null) context.Request.Headers.Cookie = $"harness_token={cookie}";
        var gate = new ControlGate(c =>
        {
            c.Response.StatusCode = 200;
            return Task.CompletedTask;
        }, o);
        await gate.InvokeAsync(context);
        return context.Response.StatusCode;
    }

    public static IEnumerable<object[]> ControlMessages()
    {
        return new[] { typeof(RestartHarness), typeof(PauseHarness), typeof(ResumeHarness), typeof(WakeHarness), typeof(TakeNewStories), typeof(RunJob) }
            .Select(t => new object[] { t });
    }

    [Theory]
    [MemberData(nameof(ControlMessages))]
    public async Task AControlRouteWithoutTheControlTokenIs401(Type message)
    {
        var e = Message(message);
        Assert.Equal(401, await Call(Options(), e));
        Assert.Equal(401, await Call(Options(), e, "wrong"));
        Assert.Equal(401, await Call(Options(), e, "lease-secret"));
        Assert.Equal(200, await Call(Options(), e, "control-secret"));
    }

    [Fact]
    public async Task AJobLogNeedsTheControlTokenFromAHeaderTheQueryOrTheCookie()
    {
        var e = Page(controlOnly: true);
        Assert.Equal(401, await Call(Options(), e, method: "GET"));
        Assert.Equal(401, await Call(Options(), e, "lease-secret", method: "GET"));
        Assert.Equal(401, await Call(Options(), e, method: "GET", query: "?token=wrong"));
        Assert.Equal(200, await Call(Options(), e, "control-secret", method: "GET"));
        Assert.Equal(200, await Call(Options(), e, method: "GET", query: "?token=control-secret"));
        Assert.Equal(200, await Call(Options(), e, method: "GET", cookie: "control-secret"));
    }

    [Fact]
    public async Task TheCookieAndTheQueryOpenNothingButAGet()
    {
        Assert.Equal(401, await Call(Options(), Message(typeof(PauseHarness)), query: "?token=control-secret"));
        Assert.Equal(401, await Call(Options(), Message(typeof(PauseHarness)), cookie: "control-secret"));
    }

    [Fact]
    public async Task WithNoControlTokenConfiguredTheControlRoutesStayShut()
    {
        Assert.Equal(401, await Call(Options(null), Message(typeof(PauseHarness)), ""));
        Assert.Equal(401, await Call(Options(null), Message(typeof(PauseHarness))));
    }

    [Fact]
    public async Task TheLeaseTokenOpensTheLeaseRoutesAndNothingElse()
    {
        var o = Options();
        var acquire = Message(typeof(AcquireBuildLease));
        Assert.Equal(200, await Call(o, acquire, "lease-secret"));
        Assert.Equal(200, await Call(o, Message(typeof(ReleaseBuildLease)), "lease-secret", method: "DELETE"));
        Assert.Equal(401, await Call(o, acquire));
        Assert.Equal(401, await Call(o, Message(typeof(RunJob)), "lease-secret"));
    }

    [Fact]
    public async Task TheReadsThePagesAndOpsUseStayOpen()
    {
        var o = Options();
        foreach (var e in new[] { Page(false), Message(typeof(GetStatus)), Message(typeof(ListJobs)), Message(typeof(GetStats)) })
        {
            Assert.Equal(200, await Call(o, e, method: "GET"));
        }
    }

    [Fact]
    public void EveryMessageOfTheSdkNeedsTheControlTokenUnlessItSaysOtherwise()
    {
        var open = new[] { typeof(GetStatus), typeof(ListJobs), typeof(GetStats) };
        var lease = new[] { typeof(AcquireBuildLease), typeof(ReleaseBuildLease) };
        var messages = typeof(RunJob).Assembly.GetTypes().Where(t => t.Namespace == typeof(RunJob).Namespace && t.GetCustomAttributes(typeof(NSail.Messaging.Annotations.HttpAttribute), false).Length > 0).ToList();
        Assert.True(messages.Count >= 11);
        foreach (var m in messages)
        {
            var expected = open.Contains(m) ? HarnessAccess.Open : lease.Contains(m) ? HarnessAccess.Lease : HarnessAccess.Control;
            Assert.Equal(expected, ControlGate.Need(Message(m)));
        }
    }

    [Fact]
    public async Task AHostTheHarnessDoesNotListenOnIsRefused()
    {
        var o = Options();
        Assert.Equal(421, await Call(o, Page(false), host: "evil.example", method: "GET"));
        Assert.Equal(200, await Call(o, Page(false), host: "localhost:6190", method: "GET"));
        o.Control.Hosts.Add("harness.example");
        Assert.Equal(200, await Call(o, Page(false), host: "harness.example", method: "GET"));
    }

    [Fact]
    public void ATokenEqualToTheLeaseTokenFailsTheStart()
    {
        var o = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "harness-settings.json")).Build()
            .GetSection(HarnessOptions.Section).Get<HarnessOptions>()!;
        o.Validate();
        o.Control = new ControlOptions { Token = "same", LeaseToken = "same" };
        Assert.Contains("the same", Assert.Throws<InvalidOperationException>(o.Validate).Message);
    }

    [Fact]
    public void AnUnsetLeaseTokenIsRandomAndStable()
    {
        var c = new ControlOptions();
        Assert.Equal(c.ResolveLeaseToken(), c.ResolveLeaseToken());
        Assert.NotEqual(c.ResolveLeaseToken(), new ControlOptions().ResolveLeaseToken());
    }
}
