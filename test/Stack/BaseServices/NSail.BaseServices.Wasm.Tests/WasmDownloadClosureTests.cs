// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;

namespace NSail.BaseServices.Wasm.Tests;

// Every Wasm client references NSail.BaseServices.Wasm, so its whole reference CLOSURE rides
// into every client's _framework whether that client calls into it or not — not only what the
// project declares (baseservices.md, Wasm bootstrap). The closure is therefore closed, and
// closed by this test rather than by the page: each entry below is a thing every Wasm client
// needs, and a reference only some need belongs to the host or kit that needs it, composed from
// that host's own Program.cs the way the push transport is.
public sealed class WasmDownloadClosureTests
{
    const string Client = "NSail.BaseServices.Wasm";

    // The reason is the rule: an entry nobody can say "every client needs this because..." of
    // does not belong here.
    static readonly Dictionary<string, string> Projects = new(StringComparer.Ordinal)
    {
        [Client] = "the project itself — AddHttpClients and WasmUrlResolver",
        ["NSail.Messaging.Http"] = "AddHttpClients: every client's remote Send goes over HTTP",
        ["NSail.Messaging.Runtime"] = "AddMessaging: the Mediator every client sends through",
        ["NSail.Messaging"] = "IMessage and IHandler, which every message in every Sdk implements",
        ["NSail.Metadata"] = "MetadataProvider, which derives every key the Runtime and the Sdks read",
        ["NSail.Types"] = "Problem, Page, Reference and the wire serialization every response carries",
        ["NSail.BclExtensions"] = "the BCL helpers the Runtime and the HTTP sender are written on",
    };

    static readonly Dictionary<string, string> Packages = new(StringComparer.Ordinal)
    {
        ["Microsoft.AspNetCore.Components.WebAssembly"] = "WebAssemblyHostBuilder — what makes a client a Wasm client, and which every client declares itself",
        ["Microsoft.Extensions.Http"] = "the HttpClient factory AddHttpClients registers the generated senders on",
        ["Microsoft.Extensions.DependencyInjection.Abstractions"] = "IServiceCollection, which every AddX extension takes",
        ["Microsoft.Extensions.Logging.Abstractions"] = "the only place a drained AfterCommit failure has to go",
    };

    [Fact]
    public void EveryProjectEveryClientDownloadsIsOneEveryClientNeeds()
    {
        AssertClosed("project", ProjectGraph.ReferenceClosure(Client), Projects);
    }

    [Fact]
    public void EveryPackageEveryClientDownloadsIsOneEveryClientNeeds()
    {
        AssertClosed("package", ProjectGraph.PackageClosure(Client), Packages);
    }

    static void AssertClosed(string kind, IReadOnlyCollection<string> measured, Dictionary<string, string> allowed)
    {
        var arrived = measured.Except(allowed.Keys, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList();
        var left = allowed.Keys.Except(measured, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList();

        Assert.True(arrived.Count == 0 && left.Count == 0, Describe(kind, arrived, left, allowed));
    }

    static string Describe(string kind, IReadOnlyList<string> arrived, IReadOnlyList<string> left, Dictionary<string, string> allowed)
    {
        var message = new StringBuilder();

        if (arrived.Count > 0)
        {
            message.AppendLine(
                $"{arrived.Count} {kind}(s) now ride into every Wasm client's download through {Client}, " +
                "and the list of what every client needs does not name them:");
            message.AppendLine();

            foreach (var name in arrived)
            {
                message.AppendLine($"  {name}");
            }

            message.AppendLine();
            message.AppendLine(
                $"Does EVERY Wasm client need it? If not, declare it on the host or kit that does " +
                $"and compose it from that host's own Program.cs, the way a push transport is " +
                $"(baseservices.md, Wasm bootstrap). If it is universal, add it here WITH the reason, " +
                "and measure what it costs a download before you do.");
        }

        if (left.Count > 0)
        {
            message.AppendLine();
            message.AppendLine(
                $"{left.Count} {kind}(s) are named here and no longer in the closure — drop them, " +
                "or the list stops describing what a client downloads:");
            message.AppendLine();

            foreach (var name in left)
            {
                message.AppendLine($"  {name} — {allowed[name]}");
            }
        }

        return message.ToString();
    }
}
