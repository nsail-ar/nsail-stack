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
        Assert.Empty(Affected("docs/guide.md", ".github/workflows/x.yml", "src/App/docs/notes.md".Replace("src/App/", "src/Docs/"))!);
    }

    [Fact]
    public void BuildConfigurationOrAnUnprovableFileBuildsEverything()
    {
        Assert.Null(Affected("Directory.Build.props"));
        Assert.Null(Affected("S.sln"));
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

    [Fact]
    public void APathIsNormalized()
    {
        Assert.Equal("src/Kit/Kit.csproj", DotNetBuilder.Normalize("src/App/../Kit/./Kit.csproj"));
    }
}
