// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Lanes;

namespace NSail.Harness.GitHub;

// A lane's scripts reach GitHub as the harness (gh reads GH_TOKEN; R names the default repository),
// except a step that runs a PR's or a branch's code and declares no token.
// A seat's gh reads an empty configuration instead: no login, so no git credential either.
public sealed class GitHubLaneEnvironment(HarnessOptions options, string token) : ILaneEnvironment
{
    public void Lane(IDictionary<string, string> env, bool credentials)
    {
        if (credentials) env["GH_TOKEN"] = token;
        else env.Remove("GH_TOKEN");
        env["R"] = options.DefaultRepository.Name;
    }

    public void Seat(IDictionary<string, string> env)
    {
        env.Remove("GH_TOKEN");
        var empty = Path.Combine(options.WorkRoot, "no-gh");
        Directory.CreateDirectory(empty);
        env["GH_CONFIG_DIR"] = empty;
    }

    // A script written for Actions reads the checkout and the scratch folder by Actions' names.
    public void Command(IDictionary<string, string> env)
    {
        if (env.TryGetValue("HARNESS_WORKSPACE", out var workspace)) env["GITHUB_WORKSPACE"] = workspace;
        if (env.TryGetValue("HARNESS_TEMP", out var temp)) env["RUNNER_TEMP"] = temp;
    }
}
