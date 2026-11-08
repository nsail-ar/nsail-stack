// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Claude;

public sealed class ClaudeOptions
{
    public const string Section = "Claude";

    public string Command { get; set; } = "claude";

    // `claude setup-token` makes it; inline, or read from a file beside the host.
    public string? OAuthToken { get; set; }
    public string? OAuthTokenFile { get; set; }

    // A model name a story or a seat gives that the CLI does not know as an alias, mapped to its id.
    // Unmapped names reach the CLI as they are: `opus`, `sonnet` and `haiku` are its own aliases for
    // the newest of each.
    public Dictionary<string, string> Models { get; set; } = [];

    // The plugins every seat installs before its run, from the marketplace main's checkout declares.
    public List<string> Plugins { get; set; } = [];

    // Claude Code settings every seat runs with (`--settings`), in a file beside the host when the
    // path is relative: what a repository's own settings do not say, for every repository.
    public string? SettingsFile { get; set; }

    // A Linux account the seats' processes run as (`sudo -n -u`): one with no ssh keys, no secrets
    // file and no docker group, so a seat's shell reaches none of the harness user's. The harness user
    // needs a sudoers rule for it with SETENV, and the account must write the slots and the config root.
    public string? RunAs { get; set; }

    public string? Settings()
    {
        if (string.IsNullOrWhiteSpace(SettingsFile)) return null;
        var path = Path.IsPathRooted(SettingsFile) ? SettingsFile : Path.Combine(AppContext.BaseDirectory, SettingsFile);
        return File.Exists(path) ? path : null;
    }

    public string ModelOf(string name)
    {
        return Models.TryGetValue(name, out var id) ? id : name;
    }

    public string? Token()
    {
        if (!string.IsNullOrWhiteSpace(OAuthToken)) return OAuthToken.Trim();
        return !string.IsNullOrWhiteSpace(OAuthTokenFile) && File.Exists(OAuthTokenFile) ? File.ReadAllText(OAuthTokenFile).Trim() : null;
    }

    // On Windows `claude` on the PATH is an npm shim (.cmd/.ps1) a process cannot be
    // started through with its stdin intact: the native claude.exe beside it is used.
    public string Resolve()
    {
        if (!OperatingSystem.IsWindows() || Path.IsPathRooted(Command)) return Command;
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var candidate in new[]
            {
                Path.Combine(dir, $"{Command}.exe"),
                Path.Combine(dir, "node_modules", "@anthropic-ai", "claude-code", "bin", "claude.exe"),
            })
            {
                if (File.Exists(candidate)) return candidate;
            }
        }
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "claude.exe");
        return File.Exists(local) ? local : Command;
    }
}
