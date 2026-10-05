// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.TypeScriptGenerator;

sealed record SdkArguments(
    string AssemblyName,
    string Output,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> References,
    IReadOnlyList<string> Defines)
{
    public static SdkArguments Read(string path)
    {
        string? assembly = null;
        string? output = null;
        var sources = new List<string>();
        var references = new List<string>();
        var defines = new List<string>();

        foreach (var line in File.ReadAllLines(path))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0)
            {
                continue;
            }

            var value = line[(separator + 1)..].Trim();

            switch (line[..separator].Trim())
            {
                case "assembly":
                    assembly = value;
                    break;
                case "out":
                    output = value;
                    break;
                case "source":
                    sources.Add(value);
                    break;
                case "reference":
                    references.Add(value);
                    break;
                case "define":
                    defines.Add(value);
                    break;
            }
        }

        if (assembly is null || output is null || sources.Count == 0)
        {
            throw new SdkReadException($"'{path}' names no assembly, no output or no source.");
        }

        return new SdkArguments(assembly, output, sources, references, defines);
    }
}
