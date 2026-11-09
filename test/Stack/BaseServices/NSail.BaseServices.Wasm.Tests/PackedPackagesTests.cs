// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;

namespace NSail.BaseServices.Wasm.Tests;

/// <summary>ci.yml packs the whole solution: a project in it is a package on nuget.org. The
/// list is the contract; a project added or dropped changes it on purpose.</summary>
public class PackedPackagesTests
{
    static readonly string[] Expected =
    [
        "NSail.Background",
        "NSail.Backup",
        "NSail.BaseServices.Wasm",
        "NSail.BaseServices.WebApi",
        "NSail.BaseServices.WebApp",
        "NSail.BclExtensions",
        "NSail.Components",
        "NSail.Components.DaisyUI",
        "NSail.Components.Mud",
        "NSail.Configuration",
        "NSail.Data",
        "NSail.Data.Testing",
        "NSail.Exporting",
        "NSail.Exporting.Excel",
        "NSail.Harness",
        "NSail.Harness.Claude",
        "NSail.Harness.GitHub",
        "NSail.Harness.Hosting",
        "NSail.Harness.OpenObserve",
        "NSail.Harness.Sdk",
        "NSail.Injection.Annotations",
        "NSail.Localization",
        "NSail.Localization.Testing",
        "NSail.Mapping",
        "NSail.Mapping.Annotations",
        "NSail.Messaging",
        "NSail.Messaging.Annotations",
        "NSail.Messaging.Http",
        "NSail.Messaging.Queue",
        "NSail.Messaging.Runtime",
        "NSail.Messaging.SignalR",
        "NSail.Messaging.Sse",
        "NSail.Messaging.WebApi",
        "NSail.Metadata",
        "NSail.Security",
        "NSail.Security.Annotations",
        "NSail.Settings",
        "NSail.SourceGeneration.Annotations",
        "NSail.SourceGeneration.Testing",
        "NSail.SourceGenerator",
        "NSail.Storage.FileSystem",
        "NSail.Storage.Git",
        "NSail.Storage.S3",
        "NSail.Storage.Sdk",
        "NSail.Storage.Shared",
        "NSail.Storage.WebApi",
        "NSail.Telemetry",
        "NSail.TypeScriptGenerator",
        "NSail.Types"
    ];

    [Fact]
    public void The_solution_packs_exactly_the_listed_packages()
    {
        var solution = File.ReadAllText(Path.Combine(ProjectGraph.RepoRoot(), "NSail.sln"));
        var packed = Regex
            .Matches(solution, @"src\\Stack\\[^""]*\\([^\\""]+)\.csproj")
            .Select(m => m.Groups[1].Value)
            .Where(id => !id.StartsWith("NSail.Sample.", StringComparison.Ordinal))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(Expected, packed);
    }

    [Fact]
    public void Every_packed_project_holds_source()
    {
        var root = Path.Combine(ProjectGraph.RepoRoot(), "src", "Stack");
        var empty = Directory
            .EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}Sample{Path.DirectorySeparatorChar}"))
            .Where(p => !Directory
                .EnumerateFiles(Path.GetDirectoryName(p)!, "*.*", SearchOption.AllDirectories)
                .Any(f => Regex.IsMatch(f, @"\.(cs|razor|js|ts|css)$") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        Assert.Empty(empty);
    }
}
