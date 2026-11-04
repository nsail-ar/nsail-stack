// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using NSail.Harness.Board;
using NSail.Harness.Locks;
using NSail.Harness.Repositories;

namespace NSail.Harness.GitHub;

// GitHub as a board (a repository's issues) and as a code host (its repositories, and the story locks
// as refs in the default one).
public sealed class GitHubProviders(GitHubClient gh, GitHubToken token) : IBoardProvider, IRepositoryProvider
{
    public string Name => "GitHub";

    public IBoard Open(IConfigurationSection board)
    {
        var options = board.Get<GitHubBoardOptions>() ?? new GitHubBoardOptions();
        if (string.IsNullOrWhiteSpace(options.Repository)) throw new InvalidOperationException($"{board.Path}: a GitHub board names its Repository");
        if (options.Project <= 0) throw new InvalidOperationException($"{board.Path}: a GitHub board names its Project, whose {options.StatusField} is the column");
        return new GitHubBoard(gh, options.Repository, options);
    }

    public IRepository Open(RepositoryOptions repository, StoryNames names)
    {
        return new GitHubRepository(gh, repository, names, token.Value);
    }

    public IStoryLock Locks(RepositoryOptions repository, string key)
    {
        return new RefLock(gh, repository.Name, key);
    }
}

// The harness's GitHub token, resolved once at the start (GitHubOptions).
public sealed record GitHubToken(string Value);
