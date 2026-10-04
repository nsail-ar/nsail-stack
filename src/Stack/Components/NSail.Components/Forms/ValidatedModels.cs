// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using System.Reflection;
using NSail.Messaging.Runtime.Validation;

namespace NSail.Components;

// What the form's validator answers for: the EditContext's own model, plus every model a
// member opened to it with [Validated] — the same reach MessageValidator takes on the wire,
// so a field draws exactly what will refuse and a row model nothing opened draws nothing.
static class ValidatedModels
{
    static readonly ConcurrentDictionary<Type, PropertyInfo[]> Members = [];

    public static bool Covers(object? root, object? model)
    {
        if (root is null || model is null)
        {
            return false;
        }

        // The common shape answers before anything is walked or allocated: a field bound to
        // the form's own model, on a model that opens nothing.
        if (Equals(root, model))
        {
            return true;
        }

        if (Declared(root.GetType()).Length == 0)
        {
            return false;
        }

        foreach (var nested in Nested(root, null))
        {
            if (Equals(nested, model))
            {
                return true;
            }
        }

        return false;
    }

    public static IEnumerable<object> Reach(object root)
    {
        yield return root;

        foreach (var nested in Nested(root, null))
        {
            yield return nested;
        }
    }

    static IEnumerable<object> Nested(object model, HashSet<object>? walked)
    {
        foreach (var property in Declared(model.GetType()))
        {
            if (property.GetValue(model) is not { } value)
            {
                continue;
            }

            // Two models declaring each other would otherwise walk until the stack ran out;
            // a model that nests nothing pays for none of this.
            walked ??= new HashSet<object>(ReferenceEqualityComparer.Instance) { model };

            if (!walked.Add(value))
            {
                continue;
            }

            yield return value;

            foreach (var deeper in Nested(value, walked))
            {
                yield return deeper;
            }
        }
    }

    // Read once per type, for RequiredMembers' own reason: every field asks on every parameter
    // set, and a form is a couple of dozen fields re-rendering on every keystroke.
    static PropertyInfo[] Declared(Type model)
    {
        return Members.GetOrAdd(
            model,
            type => [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetCustomAttribute<ValidatedAttribute>(inherit: true) is not null)]);
    }
}
