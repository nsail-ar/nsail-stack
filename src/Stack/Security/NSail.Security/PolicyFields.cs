// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using System.Reflection;
using NSail.Security.Annotations;

namespace NSail.Security;

// Reads a message's [PolicyField] values by name, through the registration the evaluator
// already binds its constraints through — never a second scan of the attributes, so a field
// the gate cannot see is a field nothing else here can read either.
//
// The properties behind the names are resolved once per message type and kept for the
// process: the registration is boot-time data and a message's shape cannot change under it.
static class PolicyFields
{
    static readonly ConcurrentDictionary<Type, PropertyInfo[]> Organizations = new();

    public static IReadOnlyCollection<Guid> OrganizationsOf(PolicyHandlerFactory factory, object message)
    {
        var properties = Organizations.GetOrAdd(message.GetType(), Declared, factory.Fields);

        if (properties.Length == 0)
        {
            return [];
        }

        var organizations = new List<Guid>();

        foreach (var property in properties)
        {
            // A Guid boxes as a Guid and a Guid? as a Guid or as null, so one case answers both
            // scalar shapes; the other is the collection the evaluator already quantifies over.
            // Guid.Empty is the non-nullable shape's null — a field nobody filled names no
            // organization, exactly as an absent one does, and the caller's own branch answers.
            switch (property.GetValue(message))
            {
                case Guid organization when organization != Guid.Empty:
                    organizations.Add(organization);
                    break;

                case IEnumerable<Guid> many:
                    organizations.AddRange(many.Where(organization => organization != Guid.Empty));
                    break;
            }
        }

        return organizations;
    }

    static PropertyInfo[] Declared(Type message, IReadOnlyDictionary<string, RestrictAs> fields)
    {
        return
        [
            .. fields
                .Where(field => field.Value == RestrictAs.Organization)
                .Select(field => message.GetProperty(field.Key, BindingFlags.Public | BindingFlags.Instance))
                .OfType<PropertyInfo>()
        ];
    }
}
