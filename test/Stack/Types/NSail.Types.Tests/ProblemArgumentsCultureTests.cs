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
}
