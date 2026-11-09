// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Shop.Nesting
{
    public class Outer
    {
        public class Inner
        {
            [Http(Get, "api/shop/nested")]
            public class GetNested : IMessage<string>
            {
            }
        }
    }
}
