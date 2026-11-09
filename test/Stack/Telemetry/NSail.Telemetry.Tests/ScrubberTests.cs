// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;

namespace NSail.Telemetry.Tests;

// The vendor instrumentations' half of the scrubbing rule. Their spans are built from a real
// request or a real DbCommand, so what is pinned here is the processor over a span carrying
// exactly the tags they are documented to write.
public sealed class ScrubberTests
{
    const string Search = "?search=Juan%20Perez&document=20123456789";

    [Fact]
    public void The_query_string_is_dropped_from_a_server_span()
    {
        var span = Ended(activity =>
        {
            activity.SetTag("url.path", "/api/directory/parties");
            activity.SetTag("url.query", Search);
        });

        Assert.Null(span.GetTagItem("url.query"));
    }

    [Fact]
    public void The_path_is_the_route_template_and_never_the_literal_values()
    {
        var span = Ended(activity =>
        {
            activity.SetTag("url.path", "/api/directory/parties/20123456789");
            activity.SetTag("http.route", "/api/directory/parties/{id}");
        });

        Assert.Equal("/api/directory/parties/{id}", span.GetTagItem("url.path"));
    }

    [Fact]
    public void A_literal_path_with_no_template_is_dropped()
    {
        var span = Ended(activity =>
        {
            activity.SetTag("url.path", "/api/directory/parties/20123456789");
        });

        Assert.Null(span.GetTagItem("url.path"));
    }

    [Fact]
    public void A_tag_off_the_allow_list_is_dropped()
    {
        var span = Ended(activity =>
        {
            activity.SetTag("enduser.id", "juan");
            activity.SetTag("user_agent.original", "Mozilla/5.0");
            activity.SetTag("x.custom", "value");
            activity.SetTag("nsail.outcome", "ok");
            activity.SetTag("http.request.method", "GET");
        });

        Assert.Equal(["http.request.method", "nsail.outcome"], span.TagObjects.Select(t => t.Key).Order().ToArray());
    }

    [Fact]
    public void A_client_spans_url_is_cut_to_its_origin()
    {
        var span = Ended(activity =>
        {
            activity.SetTag("url.full", $"https://app.nsail.ar/api/directory/parties/20123456789{Search}");
        });

        Assert.Equal("https://app.nsail.ar", span.GetTagItem("url.full"));
    }

    [Fact]
    public void A_url_with_no_path_is_left_alone()
    {
        var span = Ended(activity =>
        {
            activity.SetTag("url.full", "https://app.nsail.ar");
        });

        Assert.Equal("https://app.nsail.ar", span.GetTagItem("url.full"));
    }

    [Fact]
    public void The_sql_and_its_parameters_are_dropped()
    {
        var span = Ended(activity =>
        {
            activity.SetTag("db.system.name", "postgresql");
            activity.SetTag("db.statement", "SELECT * FROM \"Parties\" WHERE \"Name\" = 'Juan Perez'");
            activity.SetTag("db.query.text", "SELECT * FROM \"Parties\" WHERE \"Name\" = @p0");
            activity.SetTag("db.query.parameter.p0", "Juan Perez");
        });

        Assert.Null(span.GetTagItem("db.statement"));
        Assert.Null(span.GetTagItem("db.query.text"));
        Assert.Null(span.GetTagItem("db.query.parameter.p0"));
        Assert.Equal("postgresql", span.GetTagItem("db.system.name"));
    }

    static Activity Ended(Action<Activity> write)
    {
        using var source = new ActivitySource($"NSail.Telemetry.Tests.{Guid.NewGuid()}");
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate.Name == source.Name,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(listener);

        var activity = source.StartActivity("probe");

        Assert.NotNull(activity);

        write(activity);
        activity.Stop();

        using var scrubber = new Scrubber();

        scrubber.OnEnd(activity);

        return activity;
    }
}
