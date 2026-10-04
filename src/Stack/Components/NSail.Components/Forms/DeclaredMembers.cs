// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using System.Reflection;

namespace NSail.Components;

static class DeclaredMembers
{
    static readonly ConcurrentDictionary<(Type Model, string Member, Type Attribute), Attribute?> Declared = [];

    // Read once per (type, member, attribute), for RequiredMembers' own reason: every field
    // asks on every parameter set, and a form is a couple of dozen fields re-rendering on
    // every keystroke.
    public static TAttribute? On<TAttribute>(Type model, string member) where TAttribute : Attribute
    {
        var found = Declared.GetOrAdd(
            (model, member, typeof(TAttribute)),
            key => key.Model.GetProperty(key.Member)?.GetCustomAttribute(key.Attribute, inherit: true));

        return (TAttribute?)found;
    }
}
