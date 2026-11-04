// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace NSail.Components;

// NsPartial.Handoff's answers for one PersistentComponentState, shared by every instance asking
// under the same key. The state holds one value per key and throws on a second write, and its
// persisting callbacks run together, so one card mounted twice in a document (a layout that draws
// the brand in two places) would otherwise empty every handoff of the page. The prerender writes
// each key once, whichever instance read it last; the client takes each key once and hands the
// answer to each instance's first ask, for as long as one of them is mounted and none has read
// again — a card that mounts after that reads afresh.
sealed class HandoffLedger
{
    static readonly ConditionalWeakTable<PersistentComponentState, HandoffLedger> Ledgers = new();

    readonly PersistentComponentState _state;
    readonly Dictionary<string, List<(object Holder, Action Persist)>> _written = [];
    readonly Dictionary<string, (object Value, HashSet<object> Holders)> _taken = [];
    bool _registered;

    HandoffLedger(PersistentComponentState state)
    {
        _state = state;
    }

    public static HandoffLedger For(PersistentComponentState state)
    {
        return Ledgers.GetValue(state, static state => new HandoffLedger(state));
    }

    // Adopted once per instance: an instance asking again under a key it already adopted is a
    // re-read (a Reload, a Retry, a saved event) and has to send.
    public bool TryAdopt<TResult>(string key, object holder, out TResult value)
    {
        if (_taken.TryGetValue(key, out var taken))
        {
            if (taken.Value is TResult shared && taken.Holders.Add(holder))
            {
                value = shared;

                return true;
            }

            value = default!;

            return false;
        }

        if (!_state.TryTakeFromJson<TResult>(key, out var handed) || handed is null)
        {
            value = default!;

            return false;
        }

        _taken[key] = (handed, [holder]);
        value = handed;

        return true;
    }

    public void Remember<TResult>(string key, object holder, TResult result)
    {
        // A real read outdates the arrival's answer: a copy mounted after it asks rather than
        // adopting what a sibling already read past.
        _taken.Remove(key);

        // Registered once per state and never by a component, with the render mode named: the
        // reading happens at the END of the prerender, and a component that read on the way to a
        // screen it does not reach is gone by then. AuthorizeRouteView draws its Authorizing
        // placeholder inside the DEFAULT layout, so every page's arrival mounts that layout's
        // cards for as long as the session takes to resolve, and a page whose own layout is a
        // different one drops them when the answer lands. Asked for the render mode of a
        // component the renderer no longer holds, the framework dereferences a null state and
        // the whole document answers 500. WebAssembly and only WebAssembly for DeviceProvider's
        // reason: it is the one client that starts a second time in a second process, with no
        // request left to read.
        if (!_registered)
        {
            _registered = true;
            _state.RegisterOnPersisting(Persist, RenderMode.InteractiveWebAssembly);
        }

        if (!_written.TryGetValue(key, out var writers))
        {
            writers = [];
            _written[key] = writers;
        }

        // Replaced rather than added: a card that re-read before the prerender ended holds one
        // answer, its newest.
        writers.RemoveAll(writer => writer.Holder == holder);
        writers.Add((holder, () => _state.PersistAsJson(key, result)));
    }

    // A disposed instance's answers are no longer persisted, and an adopted answer nobody holds
    // any more is forgotten.
    public void Release(object holder)
    {
        foreach (var (key, writers) in _written.ToList())
        {
            if (writers.RemoveAll(writer => writer.Holder == holder) > 0 && writers.Count == 0)
            {
                _written.Remove(key);
            }
        }

        foreach (var (key, taken) in _taken.ToList())
        {
            if (taken.Holders.Remove(holder) && taken.Holders.Count == 0)
            {
                _taken.Remove(key);
            }
        }
    }

    Task Persist()
    {
        foreach (var writers in _written.Values)
        {
            writers[^1].Persist();
        }

        return Task.CompletedTask;
    }
}
