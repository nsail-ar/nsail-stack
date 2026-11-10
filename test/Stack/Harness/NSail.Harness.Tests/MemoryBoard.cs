// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging;
using NSail.Harness.Agents;

namespace NSail.Harness.Tests;

// A board kept in memory, for the jobs that only talk to the board.
sealed class MemoryBoard : IBoard
{
    public sealed class Card
    {
        public string Title = "";
        public string Body = "";
        public string Author = "";
        public string Stage = "";
        public HashSet<string> Labels = [];
        public DateTimeOffset? ClosedAt;
        public bool Muted;
        public List<StoryComment> Comments = [];
        public List<int> Blockers = [];
    }

    public Dictionary<int, Card> Cards { get; } = [];
    int _next = 100;

    public string Name => "memory";

    public Card Add(int n, string title = "a story", string body = "", string stage = "Ready", DateTimeOffset? closedAt = null, bool muted = false, params string[] labels)
    {
        var card = new Card { Title = title, Body = body, Stage = stage, ClosedAt = closedAt, Muted = muted, Labels = [.. labels] };
        Cards[n] = card;
        return card;
    }

    public Task<IReadOnlyList<Issue>> Stories(CancellationToken ct = default)
    {
        IReadOnlyList<Issue> open = Cards.Where(c => c.Value.ClosedAt is null)
            .Select(c => new Issue(c.Key, c.Value.Title, c.Value.Labels.ToHashSet(), c.Value.Stage, 0, [], -1, false, 0, 0)).ToList();
        return Task.FromResult(open);
    }

    public Task<StoryText> Story(int story, CancellationToken ct = default)
    {
        if (!Cards.TryGetValue(story, out var c)) throw new ProviderException($"no story {story}", 404);
        return Task.FromResult(new StoryText(c.Title, c.Body, c.ClosedAt is null));
    }

    public Task<string> StageOf(int story, CancellationToken ct = default)
    {
        return Task.FromResult(Cards[story].Stage);
    }

    // A provider's refusal thrown by the next SetStage or Close, once.
    public Exception? NextSetStageThrows { get; set; }

    public Exception? NextCloseThrows { get; set; }

    public Task SetStage(int story, string stage, CancellationToken ct = default)
    {
        var refused = NextSetStageThrows;
        NextSetStageThrows = null;
        if (refused is not null) throw refused;
        Cards[story].Stage = stage;
        return Task.CompletedTask;
    }

    public bool FailStageWrites { get; set; }

    public Task<bool> TrySetStage(int story, string stage, ILogger log, CancellationToken ct = default)
    {
        if (FailStageWrites) return Task.FromResult(false);
        Cards[story].Stage = stage;
        return Task.FromResult(true);
    }

    // The board does not answer a label read, as when the network is down.
    public bool LabelsDown { get; set; }

    public Task<IReadOnlySet<string>> Labels(int story, CancellationToken ct = default)
    {
        if (LabelsDown) throw new HttpRequestException("the board is down");
        return Task.FromResult<IReadOnlySet<string>>(Cards[story].Labels.ToHashSet());
    }

    public Task AddLabels(int story, params string[] labels)
    {
        Cards[story].Labels.UnionWith(labels);
        return Task.CompletedTask;
    }

    public Task RemoveLabel(int story, string label)
    {
        Cards[story].Labels.Remove(label);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> KnownLabels(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(Cards.Values.SelectMany(c => c.Labels).Distinct().ToList());
    }

    public Task<IReadOnlyDictionary<string, DateTimeOffset>> LabeledAt(int story, IReadOnlyCollection<string> labels, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyDictionary<string, DateTimeOffset>>(new Dictionary<string, DateTimeOffset>());
    }

    public Task<DateTimeOffset?> LastLift(int story, IReadOnlyCollection<string> labels, CancellationToken ct = default)
    {
        return Task.FromResult<DateTimeOffset?>(null);
    }

    public Task Comment(int story, string body)
    {
        Cards[story].Comments.Add(new StoryComment(DateTimeOffset.UtcNow, body, "harness"));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StoryComment>> Comments(int story, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<StoryComment>>(Cards[story].Comments.ToList());
    }

    public Task UpdateBody(int story, string body, CancellationToken ct = default)
    {
        Cards[story].Body = body;
        return Task.CompletedTask;
    }

    public Task Close(int story, CancellationToken ct = default)
    {
        var refused = NextCloseThrows;
        NextCloseThrows = null;
        if (refused is not null) throw refused;
        Cards[story].ClosedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<int> CreateStory(string title, string body, IReadOnlyCollection<string> labels, CancellationToken ct = default)
    {
        var n = _next++;
        Add(n, title, body, stage: "", labels: [.. labels]).Author = "harness";
        return Task.FromResult(n);
    }

    public Task<int?> FindOpen(string title, IReadOnlyCollection<string> labels, bool exact = true, CancellationToken ct = default)
    {
        return Task.FromResult(Cards.Where(c => c.Value.ClosedAt is null && (exact ? c.Value.Title == title : c.Value.Title.StartsWith(title, StringComparison.Ordinal)) && labels.All(l => c.Value.Labels.Contains(l))).Select(c => (int?)c.Key).FirstOrDefault());
    }

    public Task<IReadOnlyList<OpenStory>> OpenTitled(string prefix, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<OpenStory>>(Cards.Where(c => c.Value.ClosedAt is null && c.Value.Title.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(c => c.Key)
            .Select(c => new OpenStory(c.Key, c.Value.Title, c.Value.Author, c.Value.Labels.ToHashSet())).ToList());
    }

    public Task<IReadOnlyList<(int Number, string Stage)>> ClosedSince(DateTimeOffset since, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<(int, string)>>([]);
    }

    public Task<int> Children(int story, CancellationToken ct = default)
    {
        return Task.FromResult(0);
    }

    public Task<IReadOnlyList<FiledStory>> Labelled(string label, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<FiledStory>>(Cards.Where(c => c.Value.Labels.Contains(label))
            .Select(c => new FiledStory(c.Key, c.Value.Title, c.Value.Body, c.Value.ClosedAt, c.Value.Muted)).ToList());
    }

    public Task<IReadOnlyList<int>> BlockedBy(int story, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<int>>(Cards[story].Blockers.ToList());
    }

    public Task Block(int story, int blocker, CancellationToken ct = default)
    {
        if (!Cards.ContainsKey(blocker)) throw new ProviderException($"no story {blocker}", 404);
        Cards[story].Blockers.Add(blocker);
        return Task.CompletedTask;
    }
}

// An agent no test runs.
sealed class NoAgent : IAgent
{
    public string Driver => "none";

    public Task<AgentResult> Run(AgentRun run, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public IReadOnlyDictionary<string, string> ScriptEnvironment(string name)
    {
        return new Dictionary<string, string>();
    }

    public (bool Refused, DateTimeOffset? Until) Quota(string output)
    {
        return (false, null);
    }
}
