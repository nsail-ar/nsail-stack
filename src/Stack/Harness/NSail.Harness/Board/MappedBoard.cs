// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Board;

// A board whose columns and labels go by other names: the harness speaks its own (Stages, Labels)
// and this translates at the door, both ways, for any provider. A name the maps leave out passes
// as it is; a label key ending in `:` maps a prefix (`area:` -> `component/`).
public sealed class MappedBoard(IBoard board, IReadOnlyDictionary<string, string> stages, IReadOnlyDictionary<string, string> labels) : IBoard
{
    readonly Dictionary<string, string> _stagesBack = stages.ToDictionary(p => p.Value, p => p.Key);
    readonly Dictionary<string, string> _labelsBack = labels.ToDictionary(p => p.Value, p => p.Key);

    public string Name => board.Name;

    string StageOut(string stage)
    {
        return stages.GetValueOrDefault(stage, stage);
    }

    string StageIn(string stage)
    {
        return _stagesBack.GetValueOrDefault(stage, stage);
    }

    string LabelOut(string label)
    {
        return Translate(label, labels, labels.Where(p => p.Key.EndsWith(':')).Select(p => (p.Key, p.Value)));
    }

    string LabelIn(string label)
    {
        return Translate(label, _labelsBack, labels.Where(p => p.Key.EndsWith(':')).Select(p => (p.Value, p.Key)));
    }

    static string Translate(string label, IReadOnlyDictionary<string, string> exact, IEnumerable<(string From, string To)> prefixes)
    {
        if (exact.TryGetValue(label, out var name)) return name;
        foreach (var (from, to) in prefixes)
        {
            if (label.StartsWith(from, StringComparison.Ordinal)) return to + label[from.Length..];
        }
        return label;
    }

    IReadOnlySet<string> LabelsIn(IEnumerable<string> names)
    {
        return names.Select(LabelIn).ToHashSet();
    }

    public async Task<IReadOnlyList<Issue>> Stories(CancellationToken ct = default)
    {
        return (await board.Stories(ct)).Select(i => i with { Labels = LabelsIn(i.Labels), Stage = StageIn(i.Stage) }).ToList();
    }

    public Task<StoryText> Story(int story, CancellationToken ct = default)
    {
        return board.Story(story, ct);
    }

    public async Task<string> StageOf(int story, CancellationToken ct = default)
    {
        return StageIn(await board.StageOf(story, ct));
    }

    public Task<Uri?> Attach(int story, string name, Stream content, string contentType, CancellationToken ct = default)
    {
        return board.Attach(story, name, content, contentType, ct);
    }

    public Task SetStage(int story, string stage, CancellationToken ct = default)
    {
        return board.SetStage(story, StageOut(stage), ct);
    }

    public Task<bool> TrySetStage(int story, string stage, ILogger log, CancellationToken ct = default)
    {
        return board.TrySetStage(story, StageOut(stage), log, ct);
    }

    public async Task<IReadOnlySet<string>> Labels(int story, CancellationToken ct = default)
    {
        return LabelsIn(await board.Labels(story, ct));
    }

    public Task AddLabels(int story, params string[] labels)
    {
        return board.AddLabels(story, labels.Select(LabelOut).ToArray());
    }

    public Task RemoveLabel(int story, string label)
    {
        return board.RemoveLabel(story, LabelOut(label));
    }

    public async Task<IReadOnlyList<string>> KnownLabels(CancellationToken ct = default)
    {
        return (await board.KnownLabels(ct)).Select(LabelIn).ToList();
    }

    public async Task<IReadOnlyDictionary<string, DateTimeOffset>> LabeledAt(int story, IReadOnlyCollection<string> labels, CancellationToken ct = default)
    {
        return (await board.LabeledAt(story, labels.Select(LabelOut).ToList(), ct)).ToDictionary(p => LabelIn(p.Key), p => p.Value);
    }

    public Task<DateTimeOffset?> LastLift(int story, IReadOnlyCollection<string> labels, CancellationToken ct = default)
    {
        return board.LastLift(story, labels.Select(LabelOut).ToList(), ct);
    }

    public Task Comment(int story, string body)
    {
        return board.Comment(story, body);
    }

    public Task<IReadOnlyList<StoryComment>> Comments(int story, CancellationToken ct = default)
    {
        return board.Comments(story, ct);
    }

    public Task UpdateBody(int story, string body, CancellationToken ct = default)
    {
        return board.UpdateBody(story, body, ct);
    }

    public Task Close(int story, CancellationToken ct = default)
    {
        return board.Close(story, ct);
    }

    public Task<int> CreateStory(string title, string body, IReadOnlyCollection<string> labels, CancellationToken ct = default)
    {
        return board.CreateStory(title, body, labels.Select(LabelOut).ToList(), ct);
    }

    public Task<int?> FindOpen(string title, IReadOnlyCollection<string> labels, bool exact = true, CancellationToken ct = default)
    {
        return board.FindOpen(title, labels.Select(LabelOut).ToList(), exact, ct);
    }

    public async Task<IReadOnlyList<(int Number, string Stage)>> ClosedSince(DateTimeOffset since, CancellationToken ct = default)
    {
        return (await board.ClosedSince(since, ct)).Select(c => (c.Number, StageIn(c.Stage))).ToList();
    }

    public Task<int> Children(int story, CancellationToken ct = default)
    {
        return board.Children(story, ct);
    }

    public Task<bool> VersionClosed(string version, CancellationToken ct = default)
    {
        return board.VersionClosed(version, ct);
    }

    public Task SetVersion(int story, string version, CancellationToken ct = default)
    {
        return board.SetVersion(story, version, ct);
    }

    public Task CloseVersion(string version, CancellationToken ct = default)
    {
        return board.CloseVersion(version, ct);
    }

    public Task<IReadOnlyList<FiledStory>> Labelled(string label, CancellationToken ct = default)
    {
        return board.Labelled(LabelOut(label), ct);
    }

    public Task<IReadOnlyList<int>> BlockedBy(int story, CancellationToken ct = default)
    {
        return board.BlockedBy(story, ct);
    }

    public Task Block(int story, int blocker, CancellationToken ct = default)
    {
        return board.Block(story, blocker, ct);
    }
}
