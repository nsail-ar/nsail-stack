// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;

namespace NSail.Components;

public static class ComponentExtensions
{
    /// <summary>Wraps a delegate as an EventCallback bound to this component — the mirror
    /// of NsComponent.Raise: this constructs a callback to hand to a child, Raise invokes
    /// one a child handed back.</summary>
    public static EventCallback<TValue> Callback<TValue>(this NsComponent component, Action<TValue> action)
    {
        return EventCallback.Factory.Create(component, action);
    }
}
