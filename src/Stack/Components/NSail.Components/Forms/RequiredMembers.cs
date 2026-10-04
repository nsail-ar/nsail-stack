// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace NSail.Components;

static class RequiredMembers
{
    static readonly ConcurrentDictionary<(Type Model, string Member), bool> Declared = [];

    // Read once per (type, member): every field asks on every parameter set, and a form is a
    // couple of dozen fields re-rendering on every keystroke.
    public static bool Declares(Type model, string member)
    {
        return Declared.GetOrAdd((model, member), key =>
        {
            var property = key.Model.GetProperty(key.Member);

            if (property is null || property.GetCustomAttribute<RequiredAttribute>(inherit: true) is null)
            {
                return false;
            }

            return CanRefuse(property.PropertyType);
        });
    }

    // The attribute alone is not enough to derive a mark from. RequiredAttribute.IsValid passes
    // ANY non-null boxed value, so on a non-nullable value type — int, bool, an enum, a Guid
    // bound as Guid — it is already satisfied at the type's own default (0, false, Guid.Empty
    // all pass; measured against the BCL, not read) and refuses nothing, at either end: the
    // client's NsDataAnnotationsValidator and the wire's MessageValidator walk the same dead
    // attribute. Marking off it would draw an asterisk nothing will ever honour, which is the
    // same lie as leaving a refused field bare. Those types are marked by the field's explicit
    // Required, which mints the mark and the sentinel refusal in HandleValidationRequested off
    // one flag, so there the two cannot disagree.
    static bool CanRefuse(Type member)
    {
        return !member.IsValueType || Nullable.GetUnderlyingType(member) is not null;
    }
}
