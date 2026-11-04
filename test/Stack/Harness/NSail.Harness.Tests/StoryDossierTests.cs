// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Jobs;

namespace NSail.Harness.Tests;

// How a dead job reads back to Analyst: its last words and the plain lines around its end.
public class StoryDossierTests
{
    [Fact]
    public void ATailKeepsTheAgentsLastWordsAndTheHarnesssOwnLines()
    {
        var path = Path.GetTempFileName();
        File.WriteAllLines(path,
        [
            "== dev #1 on box1 slot 2",
            """{"type":"assistant","message":{"content":[{"type":"text","text":"Leo la story."}]}}""",
            """{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Bash"}]}}""",
            "harness: waited 300s for a build turn",
            """{"type":"result","result":"No pude compilar: falta la migración."}""",
        ]);

        var tail = StoryDossier.Tail(path);

        Assert.Contains("Leo la story.", tail);
        Assert.Contains("[result] No pude compilar: falta la migración.", tail);
        Assert.Contains("harness: waited 300s for a build turn", tail);
        Assert.DoesNotContain("tool_use", tail);
    }
}
