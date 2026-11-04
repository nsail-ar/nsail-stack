// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Jobs;

namespace NSail.Harness.Tests;

// What a red-fix cut reads back from the red story to run again on the release branch.
public class ReleaseBranchTests
{
    [Fact]
    public void TheRunnersFailedLinesNameTheTestsATheorysCasesDropped()
    {
        var output = """
              Failed NSail.Shop.E2E.WhatsAppPlanTests.With_WhatsApp_in_the_plan [1 m 3 s]
              Failed NSail.Shop.E2E.ToolBarTests.Reads_whole(width: 390) [2 s]
              Failed NSail.Shop.E2E.ToolBarTests.Reads_whole(width: 1440) [2 s]
            Passed!  - Failed: 0
            """;

        Assert.Equal(["NSail.Shop.E2E.WhatsAppPlanTests.With_WhatsApp_in_the_plan", "NSail.Shop.E2E.ToolBarTests.Reads_whole"], ReleaseJob.FailedIn(output));
    }

    [Fact]
    public void TheMarkerWinsOverTheLinesAroundIt()
    {
        var comment = "Failed Some.Other.Test [1 ms]\n\n— harness\n<!-- harness:failed A.B.C|D.E.F -->";

        Assert.Equal(["A.B.C", "D.E.F"], ReleaseJob.FailedIn(comment));
    }

    [Fact]
    public void TheFilterRunsEachByItsName()
    {
        Assert.Equal("FullyQualifiedName~A.B|FullyQualifiedName~C.D", ReleaseJob.Filter(["A.B", "C.D"]));
    }

    [Fact]
    public void TheFixesAreTheSquashesOfTheStorysPrs()
    {
        var log = "aaa test(shop): a dwell read is taken again (#2917)\nbbb fix(sales): something else (#2915)\nccc docs: (#2917) named mid-subject\n";

        Assert.Equal(["aaa"], ReleaseJob.FixesIn(log, [2917]));
    }

    [Fact]
    public void AProductsBranchesShareItsPrefix()
    {
        Assert.Equal("release/shop/", ReleaseJob.BranchPrefix("shop"));
    }
}
