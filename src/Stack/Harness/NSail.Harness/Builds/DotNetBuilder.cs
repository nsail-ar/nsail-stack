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
        return Closure(changed, solution, p => File.Exists(Path.Combine(dir, p)), p => References(dir, p));
    }

    // The pure walk over a change: Everything builds all (null); outside Roots seeds nothing when it is Markdown, else is unprovable (null); a file
    // with no owning project is prose when it is Markdown, else unprovable (null); an owner the
    // solution does not list is unprovable too.
    internal IReadOnlyList<string>? Closure(IReadOnlyList<string> changed, IReadOnlyList<string> solution, Func<string, bool> exists, Func<string, IEnumerable<string>> references)
    {
        var inSolution = solution.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seeds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in changed.Select(f => f.Replace('\\', '/')))
        {
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

    public async Task<ShellResult> Gate(string dir, string? baseSha, IDictionary<string, string> env, TimeSpan timeout, TextWriter log, CancellationToken ct = default)
    {
        var run = new Run(dir, env, DateTimeOffset.UtcNow + timeout, log, ct);
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
        return await run.Dotnet(["test", testFilter, "--configuration", options.Configuration, "--no-build", "--verbosity", "normal", .. filter]);
    }

    public async Task<ShellResult> Suite(string dir, string project, string? filter, IDictionary<string, string> env, TimeSpan timeout, TextWriter log, CancellationToken ct = default)
    {
        var run = new Run(dir, env, DateTimeOffset.UtcNow + timeout, log, ct);
        if (!File.Exists(Path.Combine(dir, project))) return run.Fail($"No suite at {project}.");
        // A build over another sha's outputs once served a mix of assemblies: the suite starts clean.
        var folder = Path.Combine(dir, Path.GetDirectoryName(project) ?? "");
        foreach (var output in new[] { "bin", "obj" }.Select(o => Path.Combine(folder, o)).Where(Directory.Exists)) Directory.Delete(output, recursive: true);

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
    sealed class Run(string dir, IDictionary<string, string> env, DateTimeOffset until, TextWriter log, CancellationToken ct)
    {
        readonly StringBuilder _output = new();

        public async Task<ShellResult> Dotnet(string[] args, IDictionary<string, string>? with = null)
        {
            var left = until - DateTimeOffset.UtcNow;
            if (left <= TimeSpan.Zero) return new ShellResult(124, _output.ToString(), TimedOut: true);
            await log.WriteLineAsync($"> dotnet {string.Join(' ', args)}");
            var r = await Shell.Run(DotnetCli, args, dir, with ?? env, left, log, ct: ct, lowPriority: true);
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
}
