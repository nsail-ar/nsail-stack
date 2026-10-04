// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Scriban;

namespace NSail.SourceGenerator.Templating;

public class SourceTemplateRegistry
{
    public static SourceTemplateRegistry Instance { get; } = new();

    readonly Dictionary<string, SourceTemplate> _templates = new();

    public SourceTemplateRegistry()
    {
        var assembly = typeof(SourceTemplateRegistry).Assembly;

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.EndsWith(".sbn", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    continue;
                }

                using (var reader = new StreamReader(stream))
                {
                    var content = reader.ReadToEnd();
                    var scribanTemplate = Template.Parse(content);

                    var name = GetTemplateName(resourceName);
                    _templates[name] = new SourceTemplate(scribanTemplate);
                }
            }
        }
    }

    public SourceTemplate Get(string name)
    {
        if (!_templates.TryGetValue(name, out var template))
            throw new InvalidOperationException($"Template {name} does not exist.");

        return template;
    }

    static string GetTemplateName(string resourceName)
    {
        var parts = resourceName.Split('.');

        if (parts.Length < 2)
        {
            throw new ArgumentException("Invalid resource name", nameof(resourceName));
        }

        var name = parts[parts.Length - 2]; // FooTemplate

        if (name.EndsWith("Template", StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - "Template".Length);
        }

        return name;
    }
}