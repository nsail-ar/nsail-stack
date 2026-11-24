// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NSail.Harness.Planning;

namespace NSail.Harness.Tests;

// The lane's dotnet shim against a harness whose turns are a real BuildLeases: every compiling verb
// waits for a turn, a run or a watch gives it back before its app starts, and what does not compile
// or already rides a held turn goes straight through.
public sealed class ShimTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("shim").FullName;
    readonly HttpListener _http = new();
    readonly CancellationTokenSource _stop = new();
    readonly Lock _gate = new();
    readonly List<string> _said = [];
    readonly Dictionary<string, Guid> _ids = [];
    readonly string _url;

    public BuildLeases Leases { get; } = new(3);

    public ShimTests()
    {
        var port = FreePort();
        _url = $"http://127.0.0.1:{port}";
        if (OperatingSystem.IsWindows()) return;
        _http.Prefixes.Add($"{_url}/");
        _http.Start();
        _ = Serve();
        // The fake dotnet says what it was asked and whether a turn was held for it.
        Executable.Write(Path.Combine(_root, "dotnet"), $"#!/usr/bin/env bash\necho \"dotnet $* held=${{HARNESS_LEASE_HELD:-}}\" >> '{Path.Combine(_root, "said")}'\n[ \"$1\" = build ] && [ -n \"${{FAIL_BUILD:-}}\" ] && exit 3\nexit 0\n");
    }

    public void Dispose()
    {
        _stop.Cancel();
        // Close alone: after a Stop, the managed listener's Close unbinds again by binding the port anew,
        // and throws when a parallel test has taken it since.
        _http.Close();
        Directory.Delete(_root, true);
    }

    [FactOn("unix")]
    public async Task ARunBuildsUnderATurnAndItsAppStartsHoldingNone()
    {
        var exit = await Shim("run", "--project", "src/Apps/Market/NSail.Market.Web", "-c", "Release", "--launch-profile", "https", "--", "--urls", "http://x");

        Assert.Equal(0, exit);
        Assert.Equal(
        [
            "take",
            "dotnet build src/Apps/Market/NSail.Market.Web -c Release -maxcpucount:4 held=1",
            "give",
            "dotnet run --no-build --project src/Apps/Market/NSail.Market.Web -c Release --launch-profile https -- --urls http://x held=",
        ], Said());
        Assert.Equal(0, Leases.Held);
    }

    [FactOn("unix")]
    public async Task AWatchBuildsUnderATurnAndGivesItBackBeforeItWatches()
    {
        var exit = await Shim("watch", "--project", "A/A.csproj", "-v", "run");

        Assert.Equal(0, exit);
        Assert.Equal(["take", "dotnet build A/A.csproj -maxcpucount:4 held=1", "give", "dotnet watch --project A/A.csproj -v run held="], Said());
    }

    // Builds 3 with three turns held: the run's build waits in line, and goes once one is given back.
    [FactOn("unix")]
    public async Task ARunWaitsBehindTheHeldTurns()
    {
        var held = new List<BuildLease>();
        for (var i = 0; i < 3; i++) held.Add(await Leases.Acquire($"other-{i}", 1, CancellationToken.None));

        var run = Shim("run", "--project", "src/Apps/Market/NSail.Market.Web");
        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.DoesNotContain(Said(), l => l.StartsWith("dotnet", StringComparison.Ordinal));
        Assert.Equal(1, Leases.Waiting);
        Leases.Release(held[0].Id);
        Assert.Equal(0, await run);
        Assert.Equal("dotnet build src/Apps/Market/NSail.Market.Web -maxcpucount:4 held=1", Said().First(l => l.StartsWith("dotnet", StringComparison.Ordinal)));
    }

    [FactOn("unix")]
    public async Task ARunWhoseBuildFailsStartsNoApp()
    {
        var exit = await ShimWith(new() { ["FAIL_BUILD"] = "1" }, "run", "--project", "A");

        Assert.Equal(3, exit);
        Assert.Equal(["take", "dotnet build A -maxcpucount:4 held=1", "give"], Said());
    }

    [FactOn("unix")]
    public async Task ARunThatDoesNotBuildTakesNoTurn()
    {
        await Shim("run", "--no-build", "--project", "A");

        Assert.Equal(["dotnet run --no-build --project A held="], Said());
    }

    [FactOn("unix")]
    public async Task ACommandThatDoesNotCompileGoesStraightThrough()
    {
        await Shim("--info");
        await Shim("tool", "restore");

        Assert.Equal(["dotnet --info held=", "dotnet tool restore held="], Said());
    }

    [FactOn("unix")]
    public async Task ADotnetUnderAHeldTurnRidesIt()
    {
        await ShimWith(new() { ["HARNESS_LEASE_HELD"] = "1" }, "publish", "Host.csproj");
        await ShimWith(new() { ["HARNESS_LEASE_HELD"] = "1" }, "run", "--project", "A");

        Assert.Equal(["dotnet publish Host.csproj -maxcpucount:4 held=1", "dotnet run --project A held=1"], Said());
    }

    [FactOn("unix")]
    public async Task ABuildHoldsItsTurnWhileItRuns()
    {
        await Shim("test", "S.sln");

        Assert.Equal(["take", "dotnet test S.sln -maxcpucount:4 held=1", "give"], Said());
    }

    Task<int> Shim(params string[] args)
    {
        return ShimWith([], args);
    }

    async Task<int> ShimWith(Dictionary<string, string> env, params string[] args)
    {
        var shim = Path.Combine(AppContext.BaseDirectory, "shims", "dotnet");
        var start = new ProcessStartInfo("bash", [shim, .. args]) { UseShellExecute = false, WorkingDirectory = _root };
        // The gate runs these tests under its own held turn: the shim must not see it.
        foreach (var name in start.Environment.Keys.Where(k => k.StartsWith("HARNESS_", StringComparison.Ordinal)).ToList()) start.Environment.Remove(name);
        start.Environment["HARNESS_REAL_DOTNET"] = Path.Combine(_root, "dotnet");
        start.Environment["HARNESS_URL"] = _url;
        start.Environment["HARNESS_BUILD_NODES"] = "4";
        start.Environment["HARNESS_HOLDER"] = "dev-1";
        foreach (var (k, v) in env) start.Environment[k] = v;
        using var p = Process.Start(start)!;
        await p.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token);
        return p.ExitCode;
    }

    // The shim's calls and the fake dotnet's lines, in the order they happened.
    List<string> Said()
    {
        var file = Path.Combine(_root, "said");
        lock (_gate)
        {
            return File.Exists(file) ? [.. File.ReadAllLines(file)] : [];
        }
    }

    void Say(string line)
    {
        lock (_gate)
        {
            File.AppendAllText(Path.Combine(_root, "said"), line + "\n");
        }
    }

    // The two routes the shim calls, over the real line of turns.
    async Task Serve()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext c;
            try
            {
                c = await _http.GetContextAsync();
            }
            catch (Exception) when (_stop.IsCancellationRequested || !_http.IsListening)
            {
                return;
            }
            _ = Task.Run(async () =>
            {
                if (c.Request.HttpMethod == "POST")
                {
                    var lease = await Leases.Acquire("dev-1", 9, _stop.Token);
                    Say("take");
                    var id = lease.Id.ToString("N");
                    lock (_gate) _ids[id] = lease.Id;
                    var body = System.Text.Encoding.UTF8.GetBytes($"\"{id}\"");
                    c.Response.ContentType = "application/json";
                    await c.Response.OutputStream.WriteAsync(body);
                }
                else
                {
                    var id = c.Request.Url!.Segments.Last();
                    Guid lease;
                    lock (_gate) lease = _ids[id];
                    Leases.Release(lease);
                    Say("give");
                    c.Response.StatusCode = 204;
                }
                c.Response.Close();
            });
        }
    }

    static int FreePort()
    {
        using var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        return ((IPEndPoint)l.LocalEndpoint).Port;
    }
}
