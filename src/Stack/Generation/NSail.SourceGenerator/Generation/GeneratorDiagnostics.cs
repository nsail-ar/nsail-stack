// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;

namespace NSail.SourceGenerator.Generation;

/// <summary>The registry of diagnostics the NSail generators report. Ids are
/// <c>NSG</c> (NSail Generator) followed by three digits, allocated sequentially and never
/// reused, so a suppression written today keeps meaning the same thing.</summary>
public static class GeneratorDiagnostics
{
    const string Category = "NSail.Generation";

    public static readonly DiagnosticDescriptor UnboundRouteToken = new(
        id: "NSG001",
        title: "Route token has no matching route property",
        messageFormat: "Route '{0}' on message '{1}' declares the token '{{{2}}}', but the message has no route-bound property named '{2}'. Add [AsRoute] to the property, or correct the token.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A token the generator cannot bind is dropped from the mapped route, so the endpoint would answer at a URL the message does not declare.");

    public static readonly DiagnosticDescriptor PushedNotAMessage = new(
        id: "NSG002",
        title: "[Pushed] on a type that is not a message",
        messageFormat: "'{0}' is marked [Pushed] but does not implement IMessage. A push rides Publish and nothing else: implement IMessage, or remove the attribute.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Neither end registers a type that cannot be published, so the attribute would sit on it and nothing would ever be pushed.");

    public static readonly DiagnosticDescriptor PushedCarriesData = new(
        id: "NSG003",
        title: "[Pushed] event carries data",
        messageFormat: "'{0}' is marked [Pushed] and declares the public property '{1}'. A pushed event reaches every seat of the tenant, whatever its branch, so it carries nothing: it says that something moved, and each client re-reads through its own org-scoped read.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The whole body of a pushed event is serialized to every connection of the tenant; a property on it is a row crossing the org wall over the socket.");

    public static readonly DiagnosticDescriptor MappedOrganizationAxis = new(
        id: "NSG004",
        title: "An organization axis is mapped by convention",
        messageFormat: "'{0}.{1}' would be written from '{2}.{1}' by name alone. A write that moves a row between branches is a decision (org-map.md, both ends): map it explicitly with [MapFrom(typeof({2}), \"{1}\")], or mark it [MapIgnore] and let the handler set it.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A message that happens to carry the branch would otherwise move the row to it in silence, past the gate's both-ends check.");

    public static readonly DiagnosticDescriptor MappedPrimitiveTypeMismatch = new(
        id: "NSG005",
        title: "[Primitive] member of a different type",
        messageFormat: "'{0}.{1}' is [Primitive], so it is assigned as it is, but its type '{2}' is not the source's '{3}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A primitive member is the source's own instance; one of another type cannot be it.");

    public static readonly DiagnosticDescriptor MappedMemberNotConvertible = new(
        id: "NSG006",
        title: "A mapped member has no conversion",
        messageFormat: "'{0}.{1}' ({2}) cannot be written from '{3}.{4}' ({5}): no conversion between the two types. Pair it with another member by [MapFrom] or [MapTo], or mark the entity's member [MapIgnore].",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Skipping the member in silence would leave the target holding a value the source said otherwise.");

    public static readonly DiagnosticDescriptor MappedTypeNotConstructible = new(
        id: "NSG007",
        title: "A mapped type has no parameterless constructor",
        messageFormat: "'{0}' is created by a generated mapper but has no accessible parameterless constructor",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A generated mapper builds new targets with new T().");

    public static readonly DiagnosticDescriptor MappedSourceNotAClass = new(
        id: "NSG008",
        title: "[MapFrom] names a type that is not a class",
        messageFormat: "'{0}' declares [MapFrom(typeof({1}))], but '{1}' is not a class",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A mapper reads a source's members; only a class has them to read.");

    public static readonly DiagnosticDescriptor ProjectedTypeUnmatched = new(
        id: "NSG009",
        title: "A projection cannot carry a derived type",
        messageFormat: "'{0}' cannot be projected as '{1}': {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A projection reads every derived type of its source; each one needs the target's derived type with the same discriminator value, under the same discriminator.");

    public static readonly DiagnosticDescriptor MappedTypeGeneric = new(
        id: "NSG010",
        title: "A mapped type is an open generic",
        messageFormat: "'{0}' declares [{1}], but it is generic: a mapper is written for one closed type, never for every T",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Generated code names its types; an open generic has no type to name.");
}
