// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using NSail.Harness.Claude;
using NSail.Harness.GitHub;
using NSail.Harness.Lanes;

namespace NSail.Harness.Tests;

public class ConfigurationTests
{
    static IConfigurationRoot Host()
    {
        return new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "harness-settings.json")).Build();
    }

    [Fact]
    public void ASampleCrewsConfigurationBindsAndValidates()
    {
        var config = Host();
        var o = config.GetSection(HarnessOptions.Section).Get<HarnessOptions>()!;

        o.Validate();
        Assert.Equal("acme/app", o.DefaultRepository.Name);
        Assert.Equal("app", o.Key);
        // The releases run on one machine, which turns them on in its Local.
        Assert.Empty(o.Releases);
        Assert.Equal(["Admin", "Clinic", "Shop"], o.Workers.Values.Where(w => w.Type == "Release").Select(w => w.Product!).Order());
        Assert.Equal("release-shop.yml", o.Workers["release-shop"].Workflow);
        Assert.True(o.Gate);
        Assert.Equal(".github/builder-prompt.md", o.Seats[o.Workers["develop"].Seat!].PromptFile);
        Assert.Equal("${vars:QA_USER}", o.Workers["test"].Variables["QA_USER"]);
        Assert.Single(config.GetSection(ClaudeOptions.Section).Get<ClaudeOptions>()!.Plugins);

        var board = config.GetSection("Harness:Board").Get<GitHubBoardOptions>()!;
        Assert.Equal(("acme/app", 1, "Status"), (board.Repository, board.Project, board.StatusField));
    }

    [Fact]
    public void TheOldListOfBoardsFailsTheStartInsteadOfRunningOnNoBoard()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Harness:Boards:0:Provider"] = "GitHub" }).Build();

        var e = Assert.Throws<InvalidOperationException>(() => new Microsoft.Extensions.DependencyInjection.ServiceCollection().AddHarness(config));
        Assert.Contains("Harness:Board", e.Message);
    }

    [Fact]
    public void ARepositoryWithoutALabelIsTheDefaultAndThereIsExactlyOne()
    {
        var o = new HarnessOptions
        {
            Repositories = [new RepositoryOptions { Name = "o/a", Label = "area:a" }, new RepositoryOptions { Name = "o/b", Label = "area:b" }],
        };

        Assert.Throws<InvalidOperationException>(o.Validate);
    }

    [Fact]
    public void ASeatTakesAPromptOrAPromptFileNotBoth()
    {
        var o = new HarnessOptions
        {
            Repositories = [new RepositoryOptions { Name = "o/a" }],
            Seats = { ["builder"] = new AgentOptions { Prompt = "build it", PromptFile = "p.md" } },
            Workers = { ["dev"] = new WorkerOptions { Type = "Develop", Seat = "builder" } },
        };

        Assert.Contains("Seats:builder", Assert.Throws<InvalidOperationException>(o.Validate).Message);
    }

    [Fact]
    public void TheStoryKeyIsTheDefaultRepositorysNameUnlessTheBoardNamesOne()
    {
        var o = new HarnessOptions { Repositories = [new RepositoryOptions { Name = "acme/shop" }] };

        Assert.Equal("shop", o.Key);
    }

    [Fact]
    public void ANewStoryIsBuiltWhereItsOnlyAreaLabelRoutesIt()
    {
        var names = new StoryNames("app", ["story", "stack"]);
        var home = new GitHubRepository(null!, new RepositoryOptions { Name = "acme/app" }, names, "t");
        var stack = new GitHubRepository(null!, new RepositoryOptions { Name = "acme/framework", BranchPrefix = "stack" }, names, "t");
        var repos = new RepositorySet([(home, new RepositoryOptions { Name = home.Name }), (stack, new RepositoryOptions { Name = stack.Name, BranchPrefix = "stack", Label = "area:stack" })]);

        Assert.Same(stack, repos.RoutedBy(["area:stack", "p1"]));
        Assert.Same(home, repos.RoutedBy(["area:stack", "area:shop"]));
        Assert.Same(home, repos.RoutedBy([]));
        Assert.Same(home, repos.Of("someone/else"));
    }

    [Theory]
    [InlineData(".github/gate.sh", "bash", ".github/gate.sh base")]
    [InlineData("tools/gate.ps1", "pwsh", "-NoProfile -NonInteractive -File tools/gate.ps1 base")]
    [InlineData("dotnet", "dotnet", "test base")]
    public void AScriptRunsUnderItsInterpreterAndAnythingElseAsItIs(string run, string file, string args)
    {
        var command = new CommandOptions { Run = run, Args = run == "dotnet" ? ["test"] : [] };

        var line = Commands.Line(command, ["base"], "bash", "pwsh");

        Assert.Equal((file, args), (line.File, string.Join(' ', line.Args)));
    }

    [Fact]
    public async Task AReferenceResolvesAndOneThatResolvesToNothingLeavesItsVariableOut()
    {
        var file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, "secret\n");
        Environment.SetEnvironmentVariable("HARNESS_TEST_VAR", "from-env");
        try
        {
            var vars = await VariableReferences.Resolve(new Dictionary<string, string>
            {
                ["PLAIN"] = "5434",
                ["REPO"] = "${vars:QA_USER}",
                ["ENV"] = "x-${env:HARNESS_TEST_VAR}",
                ["FILE"] = $"${{file:{file}}}",
                ["MISSING"] = "${vars:NOPE}",
            }, (name, _) => Task.FromResult(name == "QA_USER" ? "qa@x" : null));

            Assert.Equal("5434", vars["PLAIN"]);
            Assert.Equal("qa@x", vars["REPO"]);
            Assert.Equal("x-from-env", vars["ENV"]);
            Assert.Equal("secret", vars["FILE"]);
            Assert.False(vars.ContainsKey("MISSING"));
        }
        finally
        {
            File.Delete(file);
            Environment.SetEnvironmentVariable("HARNESS_TEST_VAR", null);
        }
    }
}
