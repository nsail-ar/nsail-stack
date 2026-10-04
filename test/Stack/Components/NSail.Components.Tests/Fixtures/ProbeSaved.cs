// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Components.Tests.Fixtures;

/// <summary>Stand-in for a UI event like PartySaved/WorkOrderSaved: published by a page hosted
/// in the aside, subscribed by a page hosted on the main surface underneath it.</summary>
public sealed class ProbeSaved : IMessage
{
}
