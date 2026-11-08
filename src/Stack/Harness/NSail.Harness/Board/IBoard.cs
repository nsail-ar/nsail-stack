// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Board;

// Where the stories live: what each one says, where it stands, what holds it and what was said about
// it. The harness is the only one that talks to it; a seat reads the story the harness lays down.
// Stage names are the pipeline's own (Stages); a board that calls them otherwise maps them.
public interface IBoard
{
    string Name { get; }

    // Every open story, as the planner reads them.
    Task<IReadOnlyList<Issue>> Stories(CancellationToken ct = default);

    Task<StoryText> Story(int story, CancellationToken ct = default);

    Task<string> StageOf(int story, CancellationToken ct = default);

    Task SetStage(int story, string stage, CancellationToken ct = default);

    // A stage move that may fail without failing the job: it says so in the log and returns false.
    Task<bool> TrySetStage(int story, string stage, ILogger log, CancellationToken ct = default);

    Task<IReadOnlySet<string>> Labels(int story, CancellationToken ct = default);

    Task AddLabels(int story, params string[] labels);

    // Removing a label the story does not carry is not an error.
    Task RemoveLabel(int story, string label);

    // Every label the board knows, whether or not a story wears it.
    Task<IReadOnlyList<string>> KnownLabels(CancellationToken ct = default);

    // When each of these labels was last put on the story; one never put on it is absent.
    Task<IReadOnlyDictionary<string, DateTimeOffset>> LabeledAt(int story, IReadOnlyCollection<string> labels, CancellationToken ct = default);

    // The last time a hand took one of these labels off: what the machine counts, it counts from there.
    Task<DateTimeOffset?> LastLift(int story, IReadOnlyCollection<string> labels, CancellationToken ct = default);

    Task Comment(int story, string body);

    Task<IReadOnlyList<StoryComment>> Comments(int story, CancellationToken ct = default);

    Task UpdateBody(int story, string body, CancellationToken ct = default);

    Task Close(int story, CancellationToken ct = default);

    // A file uploaded where a comment on the story can show it (an image inline, a video as a
    // player): its address, or null on a board that keeps no attachments.
    Task<Uri?> Attach(int story, string name, Stream content, string contentType, CancellationToken ct = default)
    {
        return Task.FromResult<Uri?>(null);
    }

    // The labels travel in the create call: a cut after it leaves a story the next pass can find.
    Task<int> CreateStory(string title, string body, IReadOnlyCollection<string> labels, CancellationToken ct = default);

    // The open story titled exactly so (exact) or whose title starts so (the oldest), if any, narrowed to those carrying
    // all of labels when given; a consistent read, never a lagging index. Identity is the title: labels are edited by hand.
    Task<int?> FindOpen(string title, IReadOnlyCollection<string> labels, bool exact = true, CancellationToken ct = default);

    // The stories closed since this instant, with the stage each one stands in: one closed outside
    // the harness (a hand, a script) keeps whatever column it had until something moves it.
    Task<IReadOnlyList<(int Number, string Stage)>> ClosedSince(DateTimeOffset since, CancellationToken ct = default);

    // How many stories hang under this one: an epic is built through its children.
    Task<int> Children(int story, CancellationToken ct = default);

    // The versions a release ships (on GitHub, a milestone named as its tag). A board that keeps
    // none answers that no version is closed and ignores the rest.
    // Closed: the version was shipped, its stories walked.
    Task<bool> VersionClosed(string version, CancellationToken ct = default)
    {
        return Task.FromResult(false);
    }

    // The story ships in the version, which is opened if the board does not name it yet.
    Task SetVersion(int story, string version, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    Task CloseVersion(string version, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    // Every story wearing the label, open or closed, with its body: how a watcher finds the story it
    // already filed. A board that cannot list them answers none, and the watcher files again.
    Task<IReadOnlyList<FiledStory>> Labelled(string label, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<FiledStory>>([]);
    }

    // The stories the board links as blocking this one, open or closed.
    Task<IReadOnlyList<int>> BlockedBy(int story, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<int>>([]);
    }

    // Links the blocker as blocking the story, where the board keeps such links.
    Task Block(int story, int blocker, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
