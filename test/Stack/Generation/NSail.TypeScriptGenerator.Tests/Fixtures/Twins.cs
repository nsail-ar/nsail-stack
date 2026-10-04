// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Shop.Left
{
    public class Twin
    {
        public int Value { get; set; }
    }

    [Http(Get, "api/shop/left")]
    public class GetLeft : IMessage<Twin>
    {
    }
}

namespace NSail.Shop.Right
{
    public class Twin
    {
        public string Value { get; set; } = string.Empty;
    }

    [Http(Get, "api/shop/right")]
    public class GetRight : IMessage<Twin>
    {
    }
}
