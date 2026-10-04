// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using NSail.Metadata;
using NSail.Problems;

namespace NSail.Telemetry;

/// <summary>The span every Mediator operation opens, and the closed list of what it is
/// allowed to carry. Nothing here reads a message's own members: a span says which
/// operation ran, how long it took and how it ended — an id, a timing and a code — so
/// there is no name, no document number and no clinical text for it to hand over.</summary>
public static class MessageSpan
{
    public const string SourceName = "NSail.Messaging";

    public const string OutcomeTag = "nsail.outcome";

    public const string ProblemTag = "nsail.problem";

    public const string Succeeded = "ok";

    public const string Refused = "refused";

    public const string Failed = "failed";

    static readonly ActivitySource _source = new(SourceName);

    /// <summary>Null when nobody is listening, which is every host that was handed no
    /// credential — the name is not even rendered in that case.</summary>
    public static Activity? Start(MetadataProvider metadata, Type messageType)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(messageType);

        if (!_source.HasListeners())
        {
            return null;
        }

        return _source.StartActivity(NameFor(metadata, messageType), ActivityKind.Internal);
    }

    public static void Succeed(Activity? activity)
    {
        activity?.SetTag(OutcomeTag, Succeeded);
    }

    public static void Fail(Activity? activity, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (activity is null)
        {
            return;
        }

        var refusal = exception as BusinessException;

        activity.SetTag(OutcomeTag, refusal is null ? Failed : Refused);

        if (refusal is not null)
        {
            activity.SetTag(ProblemTag, refusal.Code);
        }

        // Status without a description, and no RecordException: an exception message is the
        // one part of a refusal free to quote the value that caused it.
        activity.SetStatus(ActivityStatusCode.Error);
    }

    /// <summary>"{Area}.{Feature}.{Object}" — the same rendering the security gate names a
    /// message by, not MetadataProvider.KeyFor, which is the localization one and drops
    /// Feature: a span name that could not tell Directory.Parties from Directory.Contacts
    /// would collapse both into one operation.</summary>
    public static string NameFor(MetadataProvider metadata, Type messageType)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(messageType);

        var m = metadata.Get(messageType);

        return string.Join('.', new[] { m.Area, m.Feature, m.Object ?? messageType.Name }.Where(s => s is not null));
    }
}
