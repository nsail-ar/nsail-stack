// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Metadata;

namespace NSail.Messaging.Runtime.Tests;

public class MessageRegistryTests
{
    [Fact]
    public void A_nested_and_a_top_level_message_rendering_one_key_name_both()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new MessageRegistry(new MetadataProvider(), [typeof(Outer.One), typeof(One)]));

        Assert.Contains(typeof(One).FullName!, error.Message);
    }

    public class One;

    public class Outer
    {
        public class One;
    }
}
