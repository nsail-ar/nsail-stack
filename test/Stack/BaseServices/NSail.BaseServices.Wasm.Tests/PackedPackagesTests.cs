// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;

namespace NSail.BaseServices.Wasm.Tests;

// ci.yml packs the whole solution: a Stack project is a package, under its PackageId. The
// list is the contract; a project added or dropped changes it on purpose.
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
        "NSail.Messaging.SourceGenerator",
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
    public void The_stack_packs_exactly_the_listed_packages()
    {
        var packed = PackedProjects()
            .Select(p => PackageId(p))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(Expected.Order(StringComparer.Ordinal).ToList(), packed);
    }

    [Fact]
    public void Every_packed_project_is_in_the_solution()
    {
        var solution = File.ReadAllText(Path.Combine(ProjectGraph.RepoRoot(), "NSail.sln")).Replace('\\', '/');
        var missing = PackedProjects()
            .Where(p => !solution.Contains(Path.GetFileName(p), StringComparison.Ordinal))
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        Assert.Empty(missing);
    }

    // The Sample is code to read and run: src/Directory.Build.props makes it unpackable.
    static IEnumerable<string> PackedProjects()
    {
        var root = Path.Combine(ProjectGraph.RepoRoot(), "src", "Stack");
        return Directory
            .EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Replace('\\', '/').Contains("/Sample/", StringComparison.Ordinal));
    }

    static string PackageId(string csproj)
    {
        var declared = Regex.Match(File.ReadAllText(csproj), @"<PackageId>([^<]+)</PackageId>");
        return declared.Success ? declared.Groups[1].Value : Path.GetFileNameWithoutExtension(csproj);
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
