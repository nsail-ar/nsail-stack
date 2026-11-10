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
        messageFormat: "'{0}' is marked [Pushed] and declares or inherits the public property '{1}'. A pushed event reaches every seat of the tenant, whatever its branch, so it carries nothing: it says that something moved, and each client re-reads through its own org-scoped read.",
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

    public static readonly DiagnosticDescriptor SourceMemberNotMapped = new(
        id: "NSG011",
        title: "A source member is written nowhere",
        messageFormat: "'{0}.{1}' is written to no member of '{2}': the caller sends it and every save drops it. Name the entity's member for it with [MapFrom(typeof({0}), \"{1}\")], or declare the drop with Ignore = [nameof({0}.{1})] on the [MapFrom].",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The data arrives and is lost in silence, which no test of the handler can see.");

    public static readonly DiagnosticDescriptor TargetMemberNotFilled = new(
        id: "NSG012",
        title: "A target member is filled by nothing",
        messageFormat: "'{0}.{1}' is read from no member of '{2}': every row reaches the caller with it empty. Name the entity's member for it with [MapTo(typeof({0}), \"{1}\")], or declare the gap with Ignore = [nameof({0}.{1})] on the [MapTo].",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A row member nothing fills is a default the screen shows as data.");

    public static readonly DiagnosticDescriptor IgnoredMemberNotFound = new(
        id: "NSG013",
        title: "Ignore names a member that does not exist",
        messageFormat: "'{0}' declares Ignore = \"{1}\", but '{2}' has no public member named '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A name that matches nothing silences nothing, and goes on silencing nothing after the member it meant is renamed.");

    public static readonly DiagnosticDescriptor QueuedCarriesAResult = new(
        id: "NSG014",
        title: "[Queue] on a message that returns a result",
        messageFormat: "'{0}' is marked [Queue] and implements IMessage<T>. A durable send answers its caller \"accepted\" the moment the operation commits, hours before the handler runs, so there is no outcome to hand back: drop the result, or drop the attribute and let the caller wait.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The result a queued send could return is the acceptance and never the outcome; a screen that needs the outcome hears it through a [Pushed] event.");

    public static readonly DiagnosticDescriptor QueuedAlsoHttp = new(
        id: "NSG015",
        title: "[Queue] beside [Http]",
        messageFormat: "'{0}' carries both [Queue] and [Http]. Each registers an ISender for the message, so the host would hold two and every send would meet MultipleSenders at runtime: [Queue] is declared instead of [Http], not beside it.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Two senders for one message is a refusal the send pipeline can only make at runtime; the declaration is where it is visible.");

    public static readonly DiagnosticDescriptor ThrottledIdentityNotFound = new(
        id: "NSG016",
        title: "[Throttled] Identity names no string body property the middleware can read",
        messageFormat: "'{0}' declares [Throttled(Identity = \"{1}\")], but it has no public string property named '{1}' bound from the body and without [JsonPropertyName] (a route, query or header member, a non-string value or a renamed wire name is not read). Name the string property of the body that holds the account, with nameof.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The ceiling reads the JSON body; a name that matches nothing there caps no account, and goes on capping nothing after the property it meant is renamed.");

    public static readonly DiagnosticDescriptor MixedRouteSegment = new(
        id: "NSG017",
        title: "Route segment mixes a token with literal text",
        messageFormat: "Route '{0}' on message '{1}' has the segment '{2}', which mixes a token with other text. A token is a whole segment: '{{name}}', optionally with a constraint, '?' or '*'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The client writes each token as one escaped segment; a segment of literal and token together has no value to write.");

    public static readonly DiagnosticDescriptor RouteMemberWithoutToken = new(
        id: "NSG018",
        title: "[AsRoute] member has no token in the route",
        messageFormat: "'{0}.{1}' is bound from the route, but the route '{2}' has no token '{{{1}}}'. Add the token to the route, or drop [AsRoute].",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The member would be required from a route value the template never carries, and every request would fail.");

    public static readonly DiagnosticDescriptor DuplicateRoute = new(
        id: "NSG019",
        title: "Two messages share a verb and route",
        messageFormat: "'{0}' and '{1}' both answer {2} '{3}' (token names do not tell routes apart). Give one of them another route.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Both endpoints would be registered and every request to the route would fail with an ambiguous match.");
    public static readonly DiagnosticDescriptor UnknownSourceAssembly = new(
        id: "NSG020",
        title: "[Source] names an assembly the compilation does not reference",
        messageFormat: "'{0}' declares [Source(Assembly = \"{1}\")], but no assembly of that name is referenced: the holder would generate for nothing",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A scope that matches no assembly is an empty one, and an empty registration looks the same as a working one until a send meets no sender.");

    public static readonly DiagnosticDescriptor HolderNotDeclarable = new(
        id: "NSG021",
        title: "A generated holder cannot be completed",
        messageFormat: "'{0}' is marked [Generated], but its class '{1}' {2}: the generator completes a partial method of a top-level, non-generic partial class in a namespace by declaring that class again, and cannot do so here",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generated half re-declares the holder's class; a declaration that differs from the written one is a compile error in generated code nobody wrote.");

    public static readonly DiagnosticDescriptor HolderWithoutServices = new(
        id: "NSG022",
        title: "A generated holder takes no IServiceCollection",
        messageFormat: "'{0}' is marked [Generated], but none of its parameters is an IServiceCollection: there is nothing to register the generated services on",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every holder registers on the collection it is handed; without one the generated body would have no receiver.");

    public static readonly DiagnosticDescriptor HttpWithoutPath = new(
        id: "NSG023",
        title: "[Http] without a path",
        messageFormat: "'{0}' declares [Http] without a path: the message has no route to map or call",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The route is the endpoint and the client's address; a message without one is skipped by neither and served by neither.");

    public static readonly DiagnosticDescriptor CollectionHeader = new(
        id: "NSG024",
        title: "A header-bound member is a collection",
        messageFormat: "'{0}.{1}' is bound from a header, but it is a collection: the client's values reach the endpoint as one comma-joined value, never as the elements. Send it in the query or the body.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "HTTP joins repeated values of a header into one line and the endpoint binds that line as a single element, so the collection would arrive wrong or be refused.");

    public static readonly DiagnosticDescriptor MappedNullableIntoNonNullable = new(
        id: "NSG025",
        title: "A nullable member is mapped into a non-nullable one",
        messageFormat: "'{0}.{1}' ({2}) is written from '{3}.{4}' ({5}), which can hold no value, and nothing says what that becomes. Make '{0}.{1}' nullable, make '{3}.{4}' non-nullable, mark the entity's member [MapIgnore], or withdraw it from this pairing with Ignore = [nameof({3}.{4})] on the [MapFrom].",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Writing the type's default for \"no value\" would turn an absent amount into zero without a word; a key is exempt, where no value is the id still to be made. A text source is not a nullable value: a null or empty string reads the target's default.");
}
