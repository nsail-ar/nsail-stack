// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Repositories;

// Where the code lives: branches, pull requests and what was said on them, the merge, and the
// repository's own switches (workflows, variables, commit statuses). The harness alone writes here.
public interface IRepository
{
    string Name { get; }

    // The branch stories land on: every slot starts from it, every PR goes onto it.
    string DefaultBranch { get; }

    // Where a workspace clones it from, and the git credential helper that reaches it (null: none needed).
    string CloneUrl { get; }
    string? CredentialHelper { get; }

    // Every open, non-draft PR, with the CI that runs outside the harness on its head.
    Task<IReadOnlyList<OpenPr>> OpenPrs(CancellationToken ct = default);

    // Every PR of the story in this repository in this state ("open" or "merged").
    Task<IReadOnlyList<PrRef>> PrsOf(int story, string state, CancellationToken ct = default);

    Task<PullRequest> Pr(int number, CancellationToken ct = default);

    // Every review and comment on a PR, oldest first.
    Task<IReadOnlyList<PrNote>> Thread(int pr, CancellationToken ct = default);

    // A PR's files with their line counts, as `path (+a -d)`.
    Task<IReadOnlyList<string>> Files(int pr, CancellationToken ct = default);

    Task Comment(int pr, string body);

    // Opens a PR from this branch onto the default branch and returns its number.
    Task<int> CreatePr(string branch, string title, string body, CancellationToken ct = default);

    Task ClosePr(int pr, string why, CancellationToken ct = default);

    // Pushes the checked-out work to this branch of the remote, as the harness (never as a seat),
    // replacing the head it started from (null: the branch is new) and refused if the remote moved
    // past it: a rewritten branch goes up, work the voyage never saw is never overwritten.
    Task Push(string workDir, string branch, string? startedAt, CancellationToken ct = default);

    // Squash-merges and deletes the head branch; once the merge went through, nothing after it throws.
    Task Merge(int pr, CancellationToken ct = default);

    // The story's newest merge onto the default branch, if it has one.
    Task<Merge?> MergeOf(int story, CancellationToken ct = default);

    // The verdict of the CI that runs outside the harness on this head.
    Task<CiState> Ci(string branch, string sha, CancellationToken ct = default);

    Task<string> Head(string branch, CancellationToken ct = default);

    // Commit times on every branch of the story ahead of main: progress forgives a dead voyage.
    Task<IReadOnlyList<DateTimeOffset>> PushTimes(int story, CancellationToken ct = default);

    Task SetStatus(string sha, string context, string state, string description, CancellationToken ct = default);

    // The harness's verdicts live on the commits they judged: every finished status (passed or not,
    // pending left out) along the default branch, newest commit first, as far back as depth commits.
    Task<IReadOnlyList<StatusMark>> Statuses(int depth = 100, CancellationToken ct = default);

    // The workflows (by file name) with a run queued or in progress.
    Task<IReadOnlySet<string>> RunningWorkflows(CancellationToken ct = default);

    // The workflow's runs started after this instant, cancelled ones not counted.
    Task<int> RunsSince(string workflow, DateTimeOffset at, CancellationToken ct = default);

    Task Dispatch(string workflow, object inputs, CancellationToken ct = default);

    // The published releases, newest first: a tag pushed by a cut that died before publishing is not one.
    Task<IReadOnlyList<Release>> Releases(CancellationToken ct = default);

    // Whether the history of the reference (a tag, a branch) holds the commit.
    Task<bool> Holds(string reference, string sha, CancellationToken ct = default);

    Task<string?> Variable(string name, CancellationToken ct = default);
}
