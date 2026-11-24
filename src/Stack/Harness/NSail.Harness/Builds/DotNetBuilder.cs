// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.IO.Enumeration;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NSail.Harness.Lanes;

namespace NSail.Harness.Builds;

// A .NET repository's gate and suites. What a change affects is the projects owning the changed
// files plus everything that references them, transitively: the solution's own ProjectReference
// graph, read from the solution and the project files — never the disk alone, since a project on
// disk the solution does not list cannot be built through a solution filter. Anything that cannot be
// proven narrower builds the whole solution, never less.
public sealed partial class DotNetBuilder(BuildOptions options, int buildNodes) : IBuilder
{
    const string DotnetCli = "dotnet";

    public async Task<IReadOnlyList<string>?> Affected(string dir, string? baseSha, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(baseSha)) return null;
        if ((await Shell.Run("git", ["cat-file", "-e", $"{baseSha}^{{commit}}"], dir, ct: ct)).ExitCode != 0) return null;
        var diff = await Shell.Run("git", ["diff", "--name-only", baseSha, "HEAD"], dir, ct: ct);
        if (diff.ExitCode != 0) return null;
        var changed = diff.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (changed.Count == 0) return null;
        var solution = Projects(dir);
        IReadOnlyList<string>? added = null;
        if (changed.Any(IsSolution))
        {
            var before = await Shell.Run("git", ["show", $"{baseSha}:{Normalize(options.Solution)}"], dir, ct: ct, stdoutOnly: true);
            if (before.ExitCode == 0) added = AddedOnly(before.Output, await File.ReadAllTextAsync(Path.Combine(dir, options.Solution), ct), Path.GetDirectoryName(options.Solution) ?? "");
        }
        return Closure(changed, solution, p => File.Exists(Path.Combine(dir, p)), p => References(dir, p), added);
    }

    bool IsSolution(string file)
    {
        return string.Equals(Normalize(file), Normalize(options.Solution), StringComparison.OrdinalIgnoreCase);
    }

    // The pure walk over a change: Everything builds all (null); outside Roots seeds nothing when it is Markdown, else is unprovable (null); a file
    // with no owning project is prose when it is Markdown, else unprovable (null); an owner the
    // solution does not list is unprovable too. The solution itself, when its change only adds projects
    // (`added`), seeds those projects: a new kit builds itself and what references it, not the whole solution.
    internal IReadOnlyList<string>? Closure(IReadOnlyList<string> changed, IReadOnlyList<string> solution, Func<string, bool> exists, Func<string, IEnumerable<string>> references, IReadOnlyList<string>? added = null)
    {
        var inSolution = solution.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seeds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in changed.Select(f => f.Replace('\\', '/')))
        {
            if (added is not null && IsSolution(file))
            {
                if (!added.All(inSolution.Contains)) return null;
                seeds.UnionWith(added);
                continue;
            }
            var name = Path.GetFileName(file);
            if (options.Everything.Any(e => e.Contains('/') ? file.Equals(e, StringComparison.OrdinalIgnoreCase) : FileSystemName.MatchesSimpleExpression(e, name))) return null;
            var documentation = file.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
            if (options.Roots.Count > 0 && !options.Roots.Any(r => file.StartsWith(r.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)))
            {
                if (documentation) continue;
                return null;
            }
            var owner = OwnerOf(file, exists);
            if (owner is null)
            {
                if (documentation) continue;
                return null;
            }
            if (!inSolution.Contains(owner)) return null;
            seeds.Add(owner);
        }
        if (seeds.Count == 0) return [];

        var dependents = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in solution)
        {
            foreach (var reference in references(project))
            {
                if (!dependents.TryGetValue(reference, out var list)) dependents[reference] = list = [];
                list.Add(project);
            }
        }
        var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(seeds);
        while (queue.TryDequeue(out var p))
        {
            if (!affected.Add(p)) continue;
            foreach (var d in dependents.GetValueOrDefault(p) ?? []) queue.Enqueue(d);
        }
        return affected.Order(StringComparer.Ordinal).ToList();
    }

    // The project file in the nearest folder above the changed file.
    static string? OwnerOf(string file, Func<string, bool> exists)
    {
        for (var d = Path.GetDirectoryName(file)?.Replace('\\', '/'); !string.IsNullOrEmpty(d); d = Path.GetDirectoryName(d)?.Replace('\\', '/'))
        {
            var candidate = $"{d}/{Path.GetFileName(d)}.csproj";
            if (exists(candidate)) return candidate;
        }
        return null;
    }

    // The solution's projects, repository-relative with forward slashes.
    internal IReadOnlyList<string> Projects(string dir)
    {
        return ProjectsIn(File.ReadAllText(Path.Combine(dir, options.Solution)), Path.GetDirectoryName(options.Solution) ?? "");
    }

    internal static IReadOnlyList<string> ProjectsIn(string solution, string folder)
    {
        var paths = solution.TrimStart().StartsWith('<')
            ? XDocument.Parse(solution).Descendants("Project").Select(p => (string?)p.Attribute("Path")).OfType<string>()
            : SlnProject().Matches(solution).Select(m => m.Groups[1].Value);
        return paths.Where(p => p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(p => Normalize(Path.Combine(folder, p.Replace('\\', '/'))))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
    }

    // The projects a solution's change only adds, repository-relative; null when it changes anything
    // else (a project removed or moved, a configuration of one already there, the solution's own
    // settings). In a .sln an added project is its Project block and every line naming its GUID (its
    // configurations, its nesting); a solution folder added with it counts the same. The header's
    // Visual Studio version lines are the editor's, not the build's. In a .slnx it is its Project
    // element, and a Folder added with nothing else in it.
    internal static IReadOnlyList<string>? AddedOnly(string before, string after, string folder)
    {
        before = before.TrimStart('﻿');
        after = after.TrimStart('﻿');
        if (before.TrimStart().StartsWith('<') != after.TrimStart().StartsWith('<')) return null;
        var added = before.TrimStart().StartsWith('<') ? AddedToSlnx(before, after) : AddedToSln(before, after);
        if (added is null) return null;
        return added.Where(p => p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(p => Normalize(Path.Combine(folder, p.Replace('\\', '/'))))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
    }

    static List<string>? AddedToSln(string before, string after)
    {
        var old = SlnEntry().Matches(before).Select(m => m.Groups[2].Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fresh = SlnEntry().Matches(after).Where(m => !old.Contains(m.Groups[2].Value)).ToList();
        if (fresh.Count == 0) return null;
        var guids = fresh.Select(m => m.Groups[2].Value).ToList();
        var kept = new List<string>();
        var inFresh = false;
        foreach (var line in Lines(after))
        {
            if (inFresh)
            {
                if (line.Trim() == "EndProject") inFresh = false;
                continue;
            }
            if (SlnEntry().Match(line) is { Success: true } m && guids.Contains(m.Groups[2].Value, StringComparer.OrdinalIgnoreCase))
            {
                inFresh = line.Trim() != "EndProject";
                continue;
            }
            if (guids.Any(g => line.Contains(g, StringComparison.OrdinalIgnoreCase))) continue;
            kept.Add(line);
        }
        return kept.SequenceEqual(Lines(before)) ? fresh.Select(m => m.Groups[1].Value).ToList() : null;
    }

    // A solution's lines as the build reads them: no blank line, no editor version.
    static IEnumerable<string> Lines(string text)
    {
        return text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Trim().Length > 0 && !EditorVersion().IsMatch(l));
    }

    static List<string>? AddedToSlnx(string before, string after)
    {
        var old = XDocument.Parse(before);
        var now = XDocument.Parse(after);
        var paths = old.Descendants("Project").Select(p => (string?)p.Attribute("Path")).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var folders = old.Descendants("Folder").Select(f => (string?)f.Attribute("Name")).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fresh = now.Descendants("Project").Where(p => (string?)p.Attribute("Path") is { } path && !paths.Contains(path)).ToList();
        if (fresh.Count == 0) return null;
        var added = fresh.Select(p => (string)p.Attribute("Path")!).ToList();
        fresh.ForEach(p => p.Remove());
        while (now.Descendants("Folder").FirstOrDefault(f => !f.HasElements && (string?)f.Attribute("Name") is { } name && !folders.Contains(name)) is { } empty)
        {
            empty.Remove();
        }
        return XNode.DeepEquals(old.Root, now.Root) ? added : null;
    }

    // A project's ProjectReferences, repository-relative.
    static IEnumerable<string> References(string dir, string project)
    {
        var full = Path.Combine(dir, project);
        if (!File.Exists(full)) return [];
        var folder = Path.GetDirectoryName(project) ?? "";
        return XDocument.Load(full).Descendants().Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => (string?)e.Attribute("Include")).OfType<string>()
            .Select(r => Normalize(Path.Combine(folder, r.Replace('\\', '/'))))
            .ToList();
    }

    // `a/b/../c` as `a/c`, forward slashes, no leading `./`.
    internal static string Normalize(string path)
    {
        var parts = new List<string>();
        foreach (var part in path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == ".." && parts.Count > 0 && parts[^1] != "..") parts.RemoveAt(parts.Count - 1);
            else parts.Add(part);
        }
        return string.Join('/', parts);
    }

    public async Task<ShellResult> Gate(string dir, string? baseSha, IDictionary<string, string> env, TimeSpan timeout, TextWriter log, CancellationToken ct = default, string? runAs = null)
    {
        var run = new Run(dir, env, DateTimeOffset.UtcNow + timeout, log, ct, runAs);
        var affected = await Affected(dir, baseSha, ct);
        if (affected is { Count: 0 }) return run.Say("Nothing a project builds changed: green by construction.");

        if (await run.Dotnet(["restore", options.Solution, .. options.RestoreArgs]) is { ExitCode: not 0 } restore) return restore;
        var projects = affected ?? Projects(dir);
        run.Say($"Affected: {projects.Count} of {Projects(dir).Count} projects{(affected is null ? " (the whole solution)" : "")}:\n{string.Join('\n', projects)}");

        var build = Filter(dir, "affected.slnf", projects);
        if (await run.Dotnet(["build", build, "--configuration", options.Configuration, "--no-restore", Nodes]) is { ExitCode: not 0 } built) return built;

        var tests = projects.Where(p => FileSystemName.MatchesSimpleExpression(options.Tests, Path.GetFileName(p))).ToList();
        if (tests.Count == 0) return run.Say("No test project is affected by this change.");
        var testFilter = Filter(dir, "affected-tests.slnf", tests);
        string[] filter = options.TestFilter is { } f ? ["--filter", f] : [];
        // Minimal still writes every failed test and each assembly's count; a line per passed test is
        // megabytes nobody reads.
        return await run.Dotnet(["test", testFilter, "--configuration", options.Configuration, "--no-build", "--verbosity", "minimal", .. filter]);
    }

    public async Task<ShellResult> Suite(string dir, string project, string? filter, IDictionary<string, string> env, TimeSpan timeout, TextWriter log, CancellationToken ct = default, string? runAs = null)
    {
        var run = new Run(dir, env, DateTimeOffset.UtcNow + timeout, log, ct, runAs);
        if (!File.Exists(Path.Combine(dir, project))) return run.Fail($"No suite at {project}.");
        // A build over another sha's outputs once served a mix of assemblies: the suite starts clean.
        var folder = Path.Combine(dir, Path.GetDirectoryName(project) ?? "");
        foreach (var output in new[] { "bin", "obj" }.Select(o => Path.Combine(folder, o)))
        {
            await Shell.Remove(output, runAs, ct);
        }

        if (await run.Dotnet(["restore", project, .. options.RestoreArgs]) is { ExitCode: not 0 } restore) return restore;
        if (await run.Dotnet(["build", project, "--configuration", options.Configuration, "--no-restore", Nodes]) is { ExitCode: not 0 } built) return built;

        // A Playwright suite brings its browser: the library's own installer, run from its bin.
        var bin = Path.Combine(folder, "bin", options.Configuration);
        var playwright = Directory.Exists(bin) ? Directory.EnumerateFiles(bin, "Microsoft.Playwright.dll", SearchOption.AllDirectories).FirstOrDefault() : null;
        if (playwright is not null)
        {
            var name = Path.GetFileNameWithoutExtension(project);
            var at = Path.GetDirectoryName(playwright)!;
            var browser = new Dictionary<string, string>(env) { ["PLAYWRIGHT_DRIVER_SEARCH_PATH"] = at };
            if (await run.Dotnet(["exec", "--runtimeconfig", Path.Combine(at, $"{name}.runtimeconfig.json"), "--depsfile", Path.Combine(at, $"{name}.deps.json"), playwright, "install", "chromium"], browser) is { ExitCode: not 0 } installed) return installed;
        }

        string[] narrowed = filter is null ? [] : ["--filter", filter];
        // Normal names each test as it passes: a suite cut by its timeout says where it stood.
        return await run.Dotnet(["test", project, "--configuration", options.Configuration, "--no-build", "--verbosity", "normal", .. narrowed]);
    }

    string Nodes => $"-maxcpucount:{buildNodes.ToString(CultureInfo.InvariantCulture)}";

    // A solution filter over these projects, beside the solution: MSBuild resolves each one's own
    // references, and `dotnet test` takes exactly one project or solution.
    string Filter(string dir, string name, IReadOnlyList<string> projects)
    {
        var json = new StringBuilder("{\n  \"solution\": {\n    \"path\": \"").Append(Path.GetFileName(options.Solution)).Append("\",\n    \"projects\": [\n");
        json.Append(string.Join(",\n", projects.Select(p => $"      \"{Relative(p)}\""))).Append("\n    ]\n  }\n}\n");
        var at = Path.Combine(dir, Path.GetDirectoryName(options.Solution) ?? "", name);
        File.WriteAllText(at, json.ToString());
        return Path.Combine(Path.GetDirectoryName(options.Solution) ?? "", name).Replace('\\', '/');
    }

    // A project as the solution names it: relative to the solution's folder.
    string Relative(string project)
    {
        var folder = Path.GetDirectoryName(options.Solution)?.Replace('\\', '/') ?? "";
        return folder.Length > 0 && project.StartsWith(folder + "/", StringComparison.Ordinal) ? project[(folder.Length + 1)..] : project;
    }

    // The steps of one gate or suite, written to one log and answered as one result: each step's
    // output joins the whole, the first that fails ends it, and the time left is the run's.
    sealed class Run(string dir, IDictionary<string, string> env, DateTimeOffset until, TextWriter log, CancellationToken ct, string? runAs)
    {
        readonly StringBuilder _output = new();

        public async Task<ShellResult> Dotnet(string[] args, IDictionary<string, string>? with = null)
        {
            var left = until - DateTimeOffset.UtcNow;
            if (left <= TimeSpan.Zero) return new ShellResult(124, _output.ToString(), TimedOut: true);
            await log.WriteLineAsync($"> dotnet {string.Join(' ', args)}");
            var r = await Shell.Run(DotnetCli, args, dir, with ?? env, left, log, ct: ct, lowPriority: true, runAs: runAs, keep: Jobs.GateJob.IsKeptLine);
            _output.Append(r.Output);
            return r with { Output = _output.ToString() };
        }

        public ShellResult Say(string text)
        {
            log.WriteLine(text);
            _output.AppendLine(text);
            return new ShellResult(0, _output.ToString(), TimedOut: false);
        }

        public ShellResult Fail(string text)
        {
            log.WriteLine(text);
            _output.AppendLine(text);
            return new ShellResult(2, _output.ToString(), TimedOut: false);
        }
    }

    [GeneratedRegex(@"^Project\(""\{[^}]+\}""\) = ""[^""]+"", ""([^""]+\.csproj)""", RegexOptions.Multiline)]
    private static partial Regex SlnProject();

    // Any .sln entry, a project or a solution folder: its path and its GUID.
    [GeneratedRegex(@"^Project\(""\{[^}]+\}""\) = ""[^""]+"", ""([^""]+)"", ""(\{[^}]+\})""", RegexOptions.Multiline)]
    private static partial Regex SlnEntry();

    [GeneratedRegex(@"^(# Visual Studio Version |VisualStudioVersion = |MinimumVisualStudioVersion = )")]
    private static partial Regex EditorVersion();
}
