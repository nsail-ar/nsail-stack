// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.GitHub;

// A paged read still had a next link at its page cap: answering with the prefix would pass for the whole.
public sealed class GitHubPageLimitException(string path, int maxPages)
    : ProviderException($"{path}: more than {maxPages} pages; refusing to answer with a prefix")
{
}
