// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Shop.Orders;

[Http(Get, "api/shop/broken")]
public class GetBroken : IMessage<TypeNobodyDeclared>
{
}
