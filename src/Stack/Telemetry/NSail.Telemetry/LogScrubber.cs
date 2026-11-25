// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Data.Common;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace NSail.Telemetry;

/// <summary>Drops, from every log record on its way out, the runtime values the line was
/// formatted from. A span carries a closed list because NSail writes it; a log line is
/// written by any of the tree's ILogger calls and is free to hand an attribute a name, a
/// document number or a Problem's Title — and the OTLP exporter sends every attribute, the
/// exception's message and its rendered stack. What travels is what the source declares: the
/// category, the level, the event id and the message template, plus the values whose type
/// cannot carry prose, and a database failure's SqlState. The console and the file a developer reads are written before this
/// processor, so the cut costs local diagnosis nothing.</summary>
public sealed class LogScrubber : BaseProcessor<LogRecord>
{
    const string TypeTag = "exception.type";

    const string StackTag = "exception.stacktrace";

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

            if (SqlState(exception) is { } code)
            {
                kept.Add(new KeyValuePair<string, object?>(SqlStateTag, code));
            }

            // The two the exporter writes from the exception itself both carry its message —
            // the stack trace it renders is ToString(), which opens with it. The frames read
            // off StackTrace above do not.
            data.Exception = null;
        }

        data.Attributes = kept;

        // Null unless a host asked for the rendered line, and what it renders is the template
        // with every one of the values above already substituted into it.
        data.FormattedMessage = null;
    }

    // The one string read off an exception: the server's own code for the failure, which tells a
    // full server from one starting up or a refused password. It is checked against the SQL
    // standard's shape (five digits or capitals) because a provider, not the server, fills the
    // property, and a value of that shape cannot carry prose.
    static string? SqlState(Exception exception)
    {
        if (exception is DbException { SqlState: { } code } && IsSqlState(code))
        {
            return code;
        }

        IEnumerable<Exception> inner = exception is AggregateException aggregate
            ? aggregate.InnerExceptions
            : exception.InnerException is { } single ? [single] : [];

        foreach (var cause in inner)
        {
            if (SqlState(cause) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    static bool IsSqlState(string code)
    {
        return code.Length == 5 && code.All(character => char.IsAsciiDigit(character) || char.IsAsciiLetterUpper(character));
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
