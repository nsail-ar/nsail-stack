// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using NSail.Problems;

namespace NSail.Types.Tests;

public sealed class ProblemArgumentsCultureTests
{
    [Fact]
    public void Arguments_do_not_follow_the_thread_culture()
    {
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-AR");

            var stock = Assert.Single(BusinessProblem.InsufficientStock("Pen", 0.5m).Issues);
            var conflict = Assert.Single(BusinessProblem.Conflict("store", new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc)).Issues);

            Assert.Contains("0.5", stock.Message, StringComparison.Ordinal);
            Assert.Equal("2026-03-04T05:06:07Z", conflict.Arguments!["at"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void A_business_date_travels_as_an_ISO_day()
    {
        var issue = Assert.Single(BusinessProblem.AlreadyExists(typeof(string), new DateOnly(2026, 3, 4)).Issues);

        Assert.Equal("2026-03-04", issue.Arguments!["value"]);
    }

    [Fact]
    public void A_civil_time_is_not_stamped_as_UTC()
    {
        var issue = Assert.Single(BusinessProblem.AlreadyExists(typeof(string), new DateTime(2026, 3, 4, 18, 0, 0, DateTimeKind.Unspecified)).Issues);

        Assert.Equal("2026-03-04T18:00:00", issue.Arguments!["value"]);
    }
}
