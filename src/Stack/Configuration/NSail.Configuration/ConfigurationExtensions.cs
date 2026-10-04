// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace NSail.Configuration;

public static class ConfigurationExtensions
{
    public static T Load<T>(this IConfiguration configuration)
        where T : class, new()
    {
        return configuration.Load<T>(SectionName<T>());
    }

    public static T Load<T>(this IConfiguration configuration, string section)
        where T : class, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);

        var settings = new T();
        configuration.GetSection(section).Bind(settings);
        Validate(settings, section);
        return settings;
    }

    // "Options" is the family suffix (naming.md); a type that does not carry it has no
    // qualifier to drop, so its own name is already the honest section name.
    static string SectionName<T>()
    {
        const string suffix = "Options";
        var name = typeof(T).Name;

        return name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length
            ? name[..^suffix.Length]
            : name;
    }

    static void Validate<T>(T instance, string section)
        where T : class
    {
        var results = new List<ValidationResult>();

        if (Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true))
        {
            return;
        }

        var prefix = $"{section}.";
        var messages = string.Join(
            ", ",
            results.Select(result =>
            {
                var members = result.MemberNames.Any()
                    ? string.Join(", ", result.MemberNames)
                    : "configuration";

                return $"{prefix}{members}: {result.ErrorMessage}";
            }));

        throw new InvalidOperationException($"Configuration validation failed. {messages}");
    }
}
