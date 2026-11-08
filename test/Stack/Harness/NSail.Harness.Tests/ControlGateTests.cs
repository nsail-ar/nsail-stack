// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Http;
using NSail.Harness.Api;

namespace NSail.Harness.Tests;

public class ControlGateTests
{
    static HarnessOptions Options(string? control = "control-secret")
    {
        return new HarnessOptions { Control = new ControlOptions { Token = control, LeaseToken = "lease-secret" } };
    }

    static async Task<int> Call(HarnessOptions o, string method, string path, string? token = null, string host = "127.0.0.1:6190")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Host = new HostString(host);
        if (token is not null) context.Request.Headers["X-Harness-Token"] = token;
        var gate = new ControlGate(c =>
        {
            c.Response.StatusCode = 200;
            return Task.CompletedTask;
        }, o);
        await gate.InvokeAsync(context);
        return context.Response.StatusCode;
    }

    [Theory]
    [InlineData("POST", "/api/harness/restart")]
    [InlineData("POST", "/api/harness/pause")]
    [InlineData("POST", "/api/harness/resume")]
    [InlineData("POST", "/api/harness/wake")]
    [InlineData("POST", "/api/harness/take-new")]
    [InlineData("POST", "/api/harness/runs")]
    [InlineData("GET", "/jobs/abc")]
    [InlineData("GET", "/jobs/abc/log")]
    public async Task AControlRouteWithoutTheControlTokenIs401(string method, string path)
    {
        Assert.Equal(401, await Call(Options(), method, path));
        Assert.Equal(401, await Call(Options(), method, path, "wrong"));
        Assert.Equal(401, await Call(Options(), method, path, "lease-secret"));
        Assert.Equal(200, await Call(Options(), method, path, "control-secret"));
    }

    [Fact]
    public async Task WithNoControlTokenConfiguredTheControlRoutesStayShut()
    {
        Assert.Equal(401, await Call(Options(null), "POST", "/api/harness/pause", ""));
        Assert.Equal(401, await Call(Options(null), "POST", "/api/harness/pause"));
    }

    [Fact]
    public async Task TheLeaseTokenOpensTheLeaseRoutesAndNothingElse()
    {
        var o = Options();
        Assert.Equal(200, await Call(o, "POST", "/api/harness/leases", "lease-secret"));
        Assert.Equal(200, await Call(o, "DELETE", $"/api/harness/leases/{Guid.NewGuid()}", "lease-secret"));
        Assert.Equal(401, await Call(o, "POST", "/api/harness/leases"));
        Assert.Equal(401, await Call(o, "POST", "/api/harness/runs", "lease-secret"));
    }

    [Fact]
    public async Task TheReadsThePagesAndOpsUseStayOpen()
    {
        var o = Options();
        foreach (var path in new[] { "/", "/jobs", "/plan", "/api/harness/status", "/api/harness/jobs", "/api/harness/stats" })
        {
            Assert.Equal(200, await Call(o, "GET", path));
        }
    }

    [Fact]
    public async Task AHostTheHarnessDoesNotListenOnIsRefused()
    {
        var o = Options();
        Assert.Equal(421, await Call(o, "GET", "/", host: "evil.example"));
        Assert.Equal(200, await Call(o, "GET", "/", host: "localhost:6190"));
        o.Control.Hosts.Add("harness.example");
        Assert.Equal(200, await Call(o, "GET", "/", host: "harness.example"));
    }

    [Fact]
    public void AnUnsetLeaseTokenIsRandomAndStable()
    {
        var c = new ControlOptions();
        Assert.Equal(c.ResolveLeaseToken(), c.ResolveLeaseToken());
        Assert.NotEqual(c.ResolveLeaseToken(), new ControlOptions().ResolveLeaseToken());
    }
}
