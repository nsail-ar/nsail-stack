// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
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
/// cannot carry prose; an exception travels as its type and the frames of its whole cause
/// chain, with no message at any depth. The console and the file a developer reads are written before this
/// processor, so the cut costs local diagnosis nothing.</summary>
public sealed class LogScrubber : BaseProcessor<LogRecord>
{
    const string TypeTag = "exception.type";

    const string StackTag = "exception.stacktrace";

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

            if (Frames(exception) is { } frames)
            {
                kept.Add(new KeyValuePair<string, object?>(StackTag, frames));
            }

            // The two the exporter writes from the exception itself both carry its message —
            // the stack trace it renders is ToString(), which opens with it. The frames
            // rendered above do not.
            data.Exception = null;
        }

        data.Attributes = kept;

        // Null unless a host asked for the rendered line, and what it renders is the template
        // with every one of the values above already substituted into it.
        data.FormattedMessage = null;
    }

    // ToString()'s layout with every message left out, after its first line: the outer type
    // travels as exception.type. An inner exception is where a wrapped failure names its cause.
    static string? Frames(Exception exception)
    {
        var rendered = Rendered(exception);
        var first = rendered.IndexOf(Environment.NewLine, StringComparison.Ordinal);
        return first < 0 ? null : rendered[(first + Environment.NewLine.Length)..];
    }

    static string Rendered(Exception exception)
    {
        var text = new StringBuilder(exception.GetType().FullName);

        if (exception.InnerException is { } inner)
        {
            text.Append(Environment.NewLine).Append(" ---> ").Append(Rendered(inner))
                .Append(Environment.NewLine).Append("   --- End of inner exception stack trace ---");
        }

        if (exception.StackTrace is { } frames)
        {
            text.Append(Environment.NewLine).Append(frames);
        }

        if (exception is AggregateException aggregate)
        {
            for (var i = 0; i < aggregate.InnerExceptions.Count; i++)
            {
                if (aggregate.InnerExceptions[i] == aggregate.InnerException)
                {
                    continue;
                }

                text.Append(Environment.NewLine)
                    .Append(CultureInfo.InvariantCulture, $" ---> (Inner Exception #{i}) ")
                    .Append(Rendered(aggregate.InnerExceptions[i]))
                    .Append("<---")
                    .Append(Environment.NewLine);
            }
        }

        return text.ToString();
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
