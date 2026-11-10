// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Text;

namespace NSail.Types.Tests.Text;

public sealed class NamedTokensTests
{
    [Fact]
    public void Fill_replaces_in_one_pass_so_a_value_naming_a_token_stays_literal()
    {
        var values = new Dictionary<string, string> { ["name"] = "{entity}", ["entity"] = "Store" };

        Assert.Equal("{entity} / Store", NamedTokens.Fill("{name} / {entity}", values));
    }
}
