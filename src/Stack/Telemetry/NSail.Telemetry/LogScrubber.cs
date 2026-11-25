// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Data.Common;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace NSail.Telemetry;

/// <summary>Drops, from every log record on its way out, the runtime values the line was
/// formatted from. A span carries a closed list because NSail writes it; a log line is
/// written by any of the tree's ILogger calls and is free to hand an attribute a name, a
/// document number or a Problem's Title — and the OTLP exporter sends every attribute, the
/// exception's message and its rendered stack. What travels is what the source declares: the
/// category, the level, the event id and the message template, plus the values whose type
/// cannot carry prose. The console and the file a developer reads are written before this
/// processor, so the cut costs local diagnosis nothing.</summary>
public sealed class LogScrubber : BaseProcessor<LogRecord>
{
    const string TypeTag = "exception.type";

    const string StackTag = "exception.stacktrace";

    // OpenTelemetry's conventions name nothing past the outer exception. The outer keeps the
    // two tags above unchanged, so a fingerprint taken from them does not move with its cause.
    const string InnerTypeTag = "exception.inner.type";

    const string InnerStackTag = "exception.inner.stacktrace";

    // The conventions' own name for a database's status code; for PostgreSQL it is the SQLSTATE.
    const string SqlStateTag = "db.response.status_code";

    // The message template, which is the record's body and the one string that travels: it is
    // a literal at every call site, and CA2254 as an error (.editorconfig here, NSail.globalconfig in
    // the NSail.Types package for products) is what keeps it one.
    const string Template = "{OriginalFormat}";

    public override void OnEnd(LogRecord data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var kept = new List<KeyValuePair<string, object?>>();

        foreach (var attribute in data.Attributes ?? [])
        {
            if (attribute.Key == Template || Travels(attribute.Value))
            {
                kept.Add(attribute);
            }
        }

        if (data.Exception is { } exception)
        {
            kept.Add(new KeyValuePair<string, object?>(TypeTag, exception.GetType().FullName));

            if (exception.StackTrace is { } frames)
            {
                kept.Add(new KeyValuePair<string, object?>(StackTag, frames));
            }

            var chain = Chain(exception);

            if (chain.Count > 1)
            {
                kept.Add(new KeyValuePair<string, object?>(InnerTypeTag, string.Join('\n', chain.Skip(1).Select(link => link.GetType().FullName))));
                kept.Add(new KeyValuePair<string, object?>(InnerStackTag, InnerFrames(chain)));
            }

            if (chain.Select(SqlState).FirstOrDefault(state => state is not null) is { } state)
            {
                kept.Add(new KeyValuePair<string, object?>(SqlStateTag, state));
            }

            // The two the exporter writes from the exception itself both carry its message —
            // the stack trace it renders is ToString(), which opens with it and with every inner
            // exception's. The frames read off StackTrace above and in InnerFrames do not.
            data.Exception = null;
        }

        data.Attributes = kept;

        // Null unless a host asked for the rendered line, and what it renders is the template
        // with every one of the values above already substituted into it.
        data.FormattedMessage = null;
    }

    // Outermost first, each exception before its causes, and an aggregate's every cause in
    // order: its InnerException is only the first of them.
    static List<Exception> Chain(Exception outer)
    {
        var chain = new List<Exception>();
        var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<Exception>();

        pending.Push(outer);

        while (pending.TryPop(out var link))
        {
            if (!seen.Add(link))
            {
                continue;
            }

            chain.Add(link);

            IEnumerable<Exception> causes = link switch
            {
                AggregateException aggregate => aggregate.InnerExceptions,
                { InnerException: { } inner } => [inner],
                _ => [],
            };

            foreach (var cause in causes.Reverse())
            {
                pending.Push(cause);
            }
        }

        return chain;
    }

    // ToString()'s shape for the causes, with each one's header cut to its type.
    static string InnerFrames(List<Exception> chain)
    {
        var frames = new StringBuilder();

        foreach (var link in chain.Skip(1))
        {
            frames.Append(" ---> ").Append(link.GetType().FullName).Append('\n');

            if (link.StackTrace is { } trace)
            {
                frames.Append(trace).Append('\n');
            }
        }

        return frames.ToString().TrimEnd('\n');
    }

    // A SQLSTATE is five letters and digits by the standard; a provider's override that answers
    // anything else is not trusted to be free of prose.
    static string? SqlState(Exception link)
    {
        return link is DbException { SqlState: { Length: 5 } state } && state.All(char.IsAsciiLetterOrDigit)
            ? state
            : null;
    }

    // An id or a timing by type. A string is prose until something proves otherwise
    // and nothing can: a Problem's Code and a patient's name arrive here as the same type,
    // so the Code stays out of this signal and rides the span, which the traceId joins.
    static bool Travels(object? value)
    {
        return value is null
            or bool or Guid or Enum
            or DateTime or DateTimeOffset or TimeSpan or DateOnly or TimeOnly
            or byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal;
    }
}
