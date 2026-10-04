// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

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
}
