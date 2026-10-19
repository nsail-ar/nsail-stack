// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Reflection;

namespace NSail.Components;

/// <summary>Reads a skill's body from an embedded "{name}.md" file of the given assembly — the
/// server process's own file, not a wwwroot page a guide chapter is: no HTTP call, no browser,
/// readable from a handler or a background job as freely as from a web host. A kit's own
/// ISkillContributor.GetBody stays this one line; the markdown lives beside its Skills.cs as an
/// embedded resource (`&lt;EmbeddedResource Include="Skills/*.md" /&gt;`).</summary>
public static class EmbeddedSkillBody
{
    public static async Task<string> Read(Assembly assembly, string name)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var resource = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(x => x.EndsWith($".{name}.md", StringComparison.OrdinalIgnoreCase));

        if (resource is null)
        {
            throw new InvalidOperationException(
                $"No embedded resource ends with '.{name}.md' in {assembly.GetName().Name}.");
        }

        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync();
    }
}
