// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Locks;

namespace NSail.Harness.Repositories;

// A code host the configuration names (RepositoryOptions.Provider): it opens a repository, and keeps
// the story locks in the default one.
public interface IRepositoryProvider
{
    string Name { get; }

    IRepository Open(RepositoryOptions repository, StoryNames names);

    IStoryLock Locks(RepositoryOptions repository, string key);
}
