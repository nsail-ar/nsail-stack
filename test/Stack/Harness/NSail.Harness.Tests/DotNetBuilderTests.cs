// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Builds;

namespace NSail.Harness.Tests;

// What a change affects: the owning projects and every project that references them, transitively,
// or the whole solution when nothing narrower can be proven.
public class DotNetBuilderTests
{
    static readonly string[] Solution = ["src/Kit/Kit.csproj", "src/App/App.csproj", "test/Kit.Tests/Kit.Tests.csproj", "test/App.E2E/App.E2E.csproj"];

    static readonly Dictionary<string, string[]> Refs = new()
    {
        ["src/App/App.csproj"] = ["src/Kit/Kit.csproj"],
        ["test/Kit.Tests/Kit.Tests.csproj"] = ["src/Kit/Kit.csproj"],
        ["test/App.E2E/App.E2E.csproj"] = ["src/App/App.csproj"],
    };

    static IReadOnlyList<string>? Affected(params string[] changed)
    {
        var builder = new DotNetBuilder(new BuildOptions { Solution = "S.sln", Roots = ["src", "test"] }, 4);
        var onDisk = Solution.Append("src/Orphan/Orphan.csproj").ToHashSet();
        return builder.Closure(changed, Solution, onDisk.Contains, p => Refs.GetValueOrDefault(p) ?? []);
    }

    [Fact]
    public void AChangeAffectsItsProjectAndEveryoneAboveIt()
    {
        Assert.Equal(["src/App/App.csproj", "src/Kit/Kit.csproj", "test/App.E2E/App.E2E.csproj", "test/Kit.Tests/Kit.Tests.csproj"], Affected("src/Kit/Parts/Thing.cs"));
        Assert.Equal(["src/App/App.csproj", "test/App.E2E/App.E2E.csproj"], Affected("src/App/Page.razor"));
    }

    [Fact]
    public void ProseOutsideTheRootsOrOwnedByNoProjectBuildsNothing()
    {
        Assert.Empty(Affected("docs/guide.md", "src/App/docs/notes.md".Replace("src/App/", "src/Docs/"))!);
    }

    [Fact]
    public void BuildConfigurationOrAnUnprovableFileBuildsEverything()
    {
        Assert.Null(Affected("Directory.Build.props"));
        Assert.Null(Affected("S.sln"));
        Assert.Null(Affected("NuGet.Config"));
        Assert.Null(Affected(".editorconfig"));
        Assert.Null(Affected("src/Kit/.editorconfig"));
        Assert.Null(Affected("Directory.Build.rsp"));
        Assert.Null(Affected("eng/Rules.globalconfig"));
        Assert.Null(Affected(".github/workflows/x.yml"));
        Assert.Null(Affected("src/Loose/file.cs"));
        Assert.Null(Affected("src/Orphan/Orphan.cs"));
    }

    [Fact]
    public void TheSolutionNamesItsProjectsInEitherFormat()
    {
        var sln = "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Kit\", \"src\\Kit\\Kit.csproj\", \"{1}\"\nProject(\"{2150E333-8FDC-42A3-9474-1A3956D46DE8}\") = \"src\", \"src\", \"{2}\"\n";
        var slnx = "<Solution><Folder Name=\"/src/\"><Project Path=\"src/Kit/Kit.csproj\" /></Folder></Solution>";

        Assert.Equal(["src/Kit/Kit.csproj"], DotNetBuilder.ProjectsIn(sln, ""));
        Assert.Equal(["src/Kit/Kit.csproj"], DotNetBuilder.ProjectsIn(slnx, ""));
    }

    const string Before = "﻿\r\nMicrosoft Visual Studio Solution File, Format Version 12.00\r\n# Visual Studio Version 17\r\nVisualStudioVersion = 17.0.1\r\n"
        + "Project(\"{2150E333-8FDC-42A3-9474-1A3956D46DE8}\") = \"src\", \"src\", \"{00000000-0000-0000-0000-000000000001}\"\r\nEndProject\r\n"
        + "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Kit\", \"src\\Kit\\Kit.csproj\", \"{00000000-0000-0000-0000-00000000000A}\"\r\nEndProject\r\n"
        + "Global\r\n\tGlobalSection(ProjectConfigurationPlatforms) = postSolution\r\n"
        + "\t\t{00000000-0000-0000-0000-00000000000A}.Debug|Any CPU.ActiveCfg = Debug|Any CPU\r\n"
        + "\tEndGlobalSection\r\n\tGlobalSection(NestedProjects) = preSolution\r\n"
        + "\t\t{00000000-0000-0000-0000-00000000000A} = {00000000-0000-0000-0000-000000000001}\r\n"
        + "\tEndGlobalSection\r\nEndGlobal\r\n";

    // A new kit as Visual Studio writes it: a folder, two projects (one with a section of its own), their configurations and nesting.
    static string WithBooks(string text)
    {
        return text
            .Replace("VisualStudioVersion = 17.0.1", "VisualStudioVersion = 18.0.2", StringComparison.Ordinal)
            .Replace("EndProject\r\nGlobal\r\n", "EndProject\r\nProject(\"{2150E333-8FDC-42A3-9474-1A3956D46DE8}\") = \"Books\", \"Books\", \"{00000000-0000-0000-0000-000000000002}\"\r\n"
                + "\tProjectSection(SolutionItems) = preProject\r\n\t\tsrc\\Kits\\Books\\README.md = src\\Kits\\Books\\README.md\r\n\tEndProjectSection\r\nEndProject\r\n"
                + "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Books.Data\", \"src\\Kits\\Books\\Books.Data\\Books.Data.csproj\", \"{00000000-0000-0000-0000-00000000000B}\"\r\nEndProject\r\n"
                + "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Books.Tests\", \"test\\Books.Tests\\Books.Tests.csproj\", \"{00000000-0000-0000-0000-00000000000C}\"\r\nEndProject\r\n"
                + "Global\r\n", StringComparison.Ordinal)
            .Replace("\tEndGlobalSection\r\n\tGlobalSection(NestedProjects)", "\t\t{00000000-0000-0000-0000-00000000000B}.Debug|Any CPU.ActiveCfg = Debug|Any CPU\r\n"
                + "\t\t{00000000-0000-0000-0000-00000000000c}.Debug|Any CPU.ActiveCfg = Debug|Any CPU\r\n\tEndGlobalSection\r\n\tGlobalSection(NestedProjects)", StringComparison.Ordinal)
            .Replace("\tEndGlobalSection\r\nEndGlobal", "\t\t{00000000-0000-0000-0000-000000000002} = {00000000-0000-0000-0000-000000000001}\r\n"
                + "\t\t{00000000-0000-0000-0000-00000000000B} = {00000000-0000-0000-0000-000000000002}\r\n\tEndGlobalSection\r\nEndGlobal", StringComparison.Ordinal);
    }

    [Fact]
    public void ASolutionThatOnlyAddsAKitNamesTheKitsProjects()
    {
        Assert.Equal(["src/Kits/Books/Books.Data/Books.Data.csproj", "test/Books.Tests/Books.Tests.csproj"], DotNetBuilder.AddedOnly(Before, WithBooks(Before), ""));
    }

    [Fact]
    public void ASolutionThatChangesMoreThanItAddsIsUnprovable()
    {
        var reconfigured = WithBooks(Before).Replace("{00000000-0000-0000-0000-00000000000A}.Debug|Any CPU.ActiveCfg = Debug|Any CPU", "{00000000-0000-0000-0000-00000000000A}.Debug|Any CPU.ActiveCfg = Release|Any CPU", StringComparison.Ordinal);
        var removed = WithBooks(Before).Replace("Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Kit\", \"src\\Kit\\Kit.csproj\", \"{00000000-0000-0000-0000-00000000000A}\"\r\nEndProject\r\n", "", StringComparison.Ordinal);
        var moved = WithBooks(Before).Replace("{00000000-0000-0000-0000-00000000000A} = {00000000-0000-0000-0000-000000000001}", "{00000000-0000-0000-0000-00000000000A} = {00000000-0000-0000-0000-000000000002}", StringComparison.Ordinal);

        Assert.Null(DotNetBuilder.AddedOnly(Before, reconfigured, ""));
        Assert.Null(DotNetBuilder.AddedOnly(Before, removed, ""));
        Assert.Null(DotNetBuilder.AddedOnly(Before, moved, ""));
        Assert.Null(DotNetBuilder.AddedOnly(Before, WithBooks(Before).Replace("Format Version 12.00", "Format Version 12.00\r\n# a note", StringComparison.Ordinal), ""));
        Assert.Null(DotNetBuilder.AddedOnly(Before, Before, ""));
    }

    [Fact]
    public void AnSlnxThatOnlyAddsAKitNamesTheKitsProjects()
    {
        var before = "<Solution><Folder Name=\"/src/\"><Project Path=\"src/Kit/Kit.csproj\" /></Folder></Solution>";
        var after = "<Solution><Folder Name=\"/src/\"><Project Path=\"src/Kit/Kit.csproj\" /><Project Path=\"src/Books/Books.csproj\" /></Folder><Folder Name=\"/test/\"><Project Path=\"test/Books.Tests/Books.Tests.csproj\" /></Folder></Solution>";

        Assert.Equal(["src/Books/Books.csproj", "test/Books.Tests/Books.Tests.csproj"], DotNetBuilder.AddedOnly(before, after, ""));
        Assert.Null(DotNetBuilder.AddedOnly(before, after.Replace("<Project Path=\"src/Kit/Kit.csproj\" />", "", StringComparison.Ordinal), ""));
    }

    [Fact]
    public void ASolutionThatOnlyAddsAKitSeedsItsProjectsAndTheirDependents()
    {
        var builder = new DotNetBuilder(new BuildOptions { Solution = "S.sln", Roots = ["src", "test"] }, 4);
        string[] solution = [.. Solution, "src/Books/Books.csproj", "test/Books.Tests/Books.Tests.csproj"];
        var refs = new Dictionary<string, string[]>(Refs) { ["test/Books.Tests/Books.Tests.csproj"] = ["src/Books/Books.csproj"] };

        var affected = builder.Closure(["S.sln", "src/Books/Books.csproj", "src/Books/Ledger.cs", "test/Books.Tests/Books.Tests.csproj"], solution,
            solution.ToHashSet().Contains, p => refs.GetValueOrDefault(p) ?? [], added: ["src/Books/Books.csproj", "test/Books.Tests/Books.Tests.csproj"]);

        Assert.Equal(["src/Books/Books.csproj", "test/Books.Tests/Books.Tests.csproj"], affected);
        Assert.Null(builder.Closure(["S.sln"], solution, solution.ToHashSet().Contains, p => refs.GetValueOrDefault(p) ?? []));
    }

    [Fact]
    public async Task AKitAddedToTheSolutionInGitBuildsOnlyTheKit()
    {
        var dir = Path.Combine(Path.GetTempPath(), "harness-affected-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            const string project = "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>";
            Write(dir, "S.sln", Before);
            Write(dir, "src/Kit/Kit.csproj", project);
            await Lanes.Shell.Git(dir, "init", "-q", "-b", "main");
            await Lanes.Shell.Git(dir, "add", ".");
            await Lanes.Shell.Git(dir, "-c", "user.name=t", "-c", "user.email=t@example.com", "commit", "-q", "-m", "base");
            var baseSha = (await Lanes.Shell.Git(dir, "rev-parse", "HEAD")).Trim();

            Write(dir, "S.sln", WithBooks(Before));
            Write(dir, "src/Kits/Books/Books.Data/Books.Data.csproj", project);
            Write(dir, "src/Kits/Books/README.md", "# Books");
            Write(dir, "test/Books.Tests/Books.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><ProjectReference Include=\"..\\..\\src\\Kits\\Books\\Books.Data\\Books.Data.csproj\" /></ItemGroup></Project>");
            await Lanes.Shell.Git(dir, "add", ".");
            await Lanes.Shell.Git(dir, "-c", "user.name=t", "-c", "user.email=t@example.com", "commit", "-q", "-m", "a kit");

            var builder = new DotNetBuilder(new BuildOptions { Solution = "S.sln", Roots = ["src", "test"] }, 4);

            Assert.Equal(["src/Kits/Books/Books.Data/Books.Data.csproj", "test/Books.Tests/Books.Tests.csproj"], await builder.Affected(dir, baseSha));
        }
        finally
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, true);
        }
    }

    static void Write(string dir, string path, string text)
    {
        var full = Path.Combine(dir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    [Fact]
    public void APathIsNormalized()
    {
        Assert.Equal("src/Kit/Kit.csproj", DotNetBuilder.Normalize("src/App/../Kit/./Kit.csproj"));
    }
}
