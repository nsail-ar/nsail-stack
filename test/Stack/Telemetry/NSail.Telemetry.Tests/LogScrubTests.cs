// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace NSail.Telemetry.Tests;

// The logs signal's half of the scrubbing rule. What a span refuses to carry a log line is
// free to write down — ErrorMiddleware logs "Exception mapped to problem {Code}: {Message}"
// and for a refusal that {Message} is the Problem's Title — so the same cut is made on the
// way to the exporter, over the record any ILogger in the tree produces.
public sealed class LogScrubTests
{
    const string Name = "Juan Perez";

    const string Document = "20123456789";

    const string Clinical = "refiere cefalea desde marzo";

    const string ErrorTemplate = "Exception mapped to problem {Code}: {Message}";

    const string Category = "Consulting";

    [Fact]
    public void The_values_a_line_was_formatted_from_do_not_travel()
    {
        var record = Scrubbed(logger => logger.LogWarning(ErrorTemplate, "PatientRefused", $"{Name} may not be archived"));

        Assert.Equal(ErrorTemplate, record.Body);
        Assert.Null(record.Formatted);
        Assert.DoesNotContain(Name, record.Flattened, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_exception_travels_as_its_type_and_its_frames_and_never_as_its_message()
    {
        var record = Scrubbed(logger => logger.LogError(
            Raised($"{Document} is already registered"),
            "Unhandled exception mapped to problem {Code}",
            "Unhandled"));

        var stack = Assert.IsType<string>(record.Tag("exception.stacktrace"));

        Assert.False(record.HadException);
        Assert.Equal(typeof(InvalidOperationException).FullName, record.Tag("exception.type"));
        Assert.Contains(nameof(Raised), stack, StringComparison.Ordinal);
        Assert.DoesNotContain(Document, record.Flattened, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Nothing_a_message_carries_reaches_a_log_record()
    {
        var record = Scrubbed(logger => logger.LogInformation(
            "Patient {Id} was created with {Name}, {Document} and {Notes}",
            Guid.Empty,
            Name,
            Document,
            Clinical));

        Assert.DoesNotContain(Name, record.Flattened, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Document, record.Flattened, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Clinical, record.Flattened, StringComparison.OrdinalIgnoreCase);
    }

    // An id and a timing are what the story leaves a log free to carry, and their types are
    // what say so — nothing in the tree can hand a Guid or a TimeSpan a name. A code cannot
    // be told from prose that way, so it rides the span instead, which the traceId joins.
    [Fact]
    public void An_id_and_a_timing_survive_and_a_code_does_not()
    {
        var id = Guid.NewGuid();

        var record = Scrubbed(logger => logger.LogInformation(
            "Job {Job} ran {Count} times for {Id} in {Elapsed}",
            "HorizonJob",
            3,
            id,
            TimeSpan.FromSeconds(2)));

        Assert.Equal(id, record.Tag("Id"));
        Assert.Equal(3, record.Tag("Count"));
        Assert.Equal(TimeSpan.FromSeconds(2), record.Tag("Elapsed"));
        Assert.Null(record.Tag("Job"));
    }

    // The host's own pipeline, built by AddBaseWebApi and nothing else: a processor added
    // behind it — which is where the OTLP exporter sits — is handed a scrubbed record.
    [Fact]
    public void The_host_scrubs_what_reaches_the_exporters_place_in_the_chain()
    {
        var records = new LogRecorder();

        using var app = Harness.Host(credentialed: true, logging: logging => logging.AddProcessor(records));

        app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(Category)
            .LogWarning(ErrorTemplate, "PatientRefused", $"{Name} may not be archived");

        var record = Assert.Single(records.Seen.Where(seen => seen.Category == Category));

        Assert.Equal(ErrorTemplate, record.Body);
        Assert.DoesNotContain(Name, record.Flattened, StringComparison.OrdinalIgnoreCase);
    }

    static Seen Scrubbed(Action<ILogger> write)
    {
        var records = new LogRecorder();

        using var factory = LoggerFactory.Create(logging =>
        {
            logging.AddOpenTelemetry(telemetry =>
            {
                telemetry.AddProcessor(new LogScrubber());
                telemetry.AddProcessor(records);
            });
        });

        write(factory.CreateLogger(Category));

        return Assert.Single(records.Seen);
    }

    static Exception Raised(string message)
    {
        try
        {
            throw new InvalidOperationException(message);
        }
        catch (InvalidOperationException thrown)
        {
            return thrown;
        }
    }

    // LogRecord instances are pooled and reused, so what a test keeps is what it read on the
    // way out, never the record itself.
    sealed record Seen(
        string? Category,
        string? Body,
        string? Formatted,
        bool HadException,
        IReadOnlyList<KeyValuePair<string, object?>> Attributes,
        string Flattened)
    {
        public object? Tag(string key)
        {
            return Attributes.FirstOrDefault(attribute => attribute.Key == key).Value;
        }
    }

    sealed class LogRecorder : BaseProcessor<LogRecord>
    {
        public List<Seen> Seen { get; } = [];

        public override void OnEnd(LogRecord data)
        {
            ArgumentNullException.ThrowIfNull(data);

            var attributes = (data.Attributes ?? []).ToList();

            var flattened = string.Join(
                '|',
                attributes.Select(attribute => $"{attribute.Key}={attribute.Value}")
                    .Append(data.Body)
                    .Append(data.FormattedMessage)
                    .Append(data.Exception?.ToString()));

            Seen.Add(new Seen(data.CategoryName, data.Body, data.FormattedMessage, data.Exception is not null, attributes, flattened));
        }
    }
}
