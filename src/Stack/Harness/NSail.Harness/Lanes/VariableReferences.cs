// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;

namespace NSail.Harness.Lanes;

// A configured variable's value: literal, or built from references — `${vars:NAME}` the default
// repository's variable, `${env:NAME}` this process's environment, `${file:PATH}` a file's text. A
// variable with a reference that resolves to nothing is left out, never set half-empty.
public static partial class VariableReferences
{
    public static async Task<Dictionary<string, string>> Resolve(
        IEnumerable<KeyValuePair<string, string>> variables, Func<string, CancellationToken, Task<string?>> repositoryVariable, CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>();
        foreach (var (name, value) in variables)
        {
            if (await Resolve(value, repositoryVariable, ct) is { } resolved) result[name] = resolved;
        }
        return result;
    }

    public static async Task<string?> Resolve(string value, Func<string, CancellationToken, Task<string?>> repositoryVariable, CancellationToken ct = default)
    {
        var text = value;
        foreach (var m in Reference().Matches(value).Cast<Match>().Reverse())
        {
            var key = m.Groups["key"].Value;
            var resolved = m.Groups["kind"].Value switch
            {
                "vars" => await repositoryVariable(key, ct),
                "env" => Environment.GetEnvironmentVariable(key),
                _ => File.Exists(key) ? (await File.ReadAllTextAsync(key, ct)).Trim() : null,
            };
            if (resolved is null) return null;
            text = text[..m.Index] + resolved + text[(m.Index + m.Length)..];
        }
        return text;
    }

    [GeneratedRegex(@"\$\{(?<kind>vars|env|file):(?<key>[^}]+)\}")]
    private static partial Regex Reference();
}
