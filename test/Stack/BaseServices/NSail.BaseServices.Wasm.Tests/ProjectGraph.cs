// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Xml.Linq;

namespace NSail.BaseServices.Wasm.Tests;

/// <summary>The Stack's csproj graph as text. A rule about what a browser DOWNLOADS cannot be
/// read off a loaded assembly: the test host runs on net10.0 and resolves its own closure, so
/// the only place the client's closure exists is the project files themselves.</summary>
internal static class ProjectGraph
{
    /// <summary>Every project reachable from the one named, itself included, by
    /// <c>ProjectReference</c> — the set whose output rides into a download together. An
    /// analyzer reference is not one of them: <c>ReferenceOutputAssembly=false</c> keeps its
    /// assembly out of the build's output, which is the whole point of that metadata.</summary>
    internal static IReadOnlyCollection<string> ReferenceClosure(string project)
    {
        var start = Locate(project);
        var closure = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>([start]);

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();

            if (!closure.Add(current))
            {
                continue;
            }

            foreach (var reference in ShippingProjectReferences(current))
            {
                pending.Enqueue(reference);
            }
        }

        return closure.Select(NameOf).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Every NuGet package the closure declares, which is every package a client
    /// restores because of it. A package carried only for the build — an analyzer, a test
    /// collector — is marked <c>PrivateAssets=all</c> and reaches no consumer.</summary>
    internal static IReadOnlyCollection<string> PackageClosure(string project)
    {
        var packages = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in ReferenceClosure(project))
        {
            foreach (var package in DeclaredPackages(Locate(member)))
            {
                packages.Add(package);
            }
        }

        return packages;
    }

    internal static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }

    static string Locate(string project)
    {
        var matches = Directory
            .EnumerateFiles(Path.Combine(RepoRoot(), "src", "Stack"), $"{project}.csproj", SearchOption.AllDirectories)
            .ToList();

        return matches.Count == 1
            ? matches[0]
            : throw new FileNotFoundException($"{matches.Count} project files are named {project}.csproj under src/Stack.");
    }

    static IEnumerable<string> ShippingProjectReferences(string csproj)
    {
        var directory = Path.GetDirectoryName(csproj)!;

        foreach (var reference in XDocument.Load(csproj).Descendants("ProjectReference"))
        {
            var include = (string?)reference.Attribute("Include");

            if (include == null || DropsItsOutput(reference))
            {
                continue;
            }

            yield return Path.GetFullPath(Path.Combine(directory, include.Replace('\\', Path.DirectorySeparatorChar)));
        }
    }

    static IEnumerable<string> DeclaredPackages(string csproj)
    {
        foreach (var reference in XDocument.Load(csproj).Descendants("PackageReference"))
        {
            var include = (string?)reference.Attribute("Include");

            if (include == null || IsBuildOnly(reference))
            {
                continue;
            }

            yield return include;
        }
    }

    static bool DropsItsOutput(XElement reference)
    {
        return string.Equals(Metadata(reference, "ReferenceOutputAssembly"), "false", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsBuildOnly(XElement reference)
    {
        return string.Equals(Metadata(reference, "PrivateAssets"), "all", StringComparison.OrdinalIgnoreCase);
    }

    // MSBuild reads item metadata written either as a child element or as an attribute on the
    // item, so a rule that only reads one spelling is blind to half of them.
    static string? Metadata(XElement reference, string name)
    {
        return (string?)reference.Element(name) ?? (string?)reference.Attribute(name);
    }

    static string NameOf(string csproj)
    {
        return Path.GetFileNameWithoutExtension(csproj);
    }
}
