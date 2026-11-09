// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>Scoped source of the current Session. The default is anonymous — Iam
/// replaces it server-side with one built from the request's principal; jobs set
/// Session.System(); tests set whatever they need.</summary>
public class SessionProvider : IDisposable
{
    // An ASSIGNED session travels with the async flow as well as with the instance. The
    // outermost in-process Send runs in a service scope of its own and everything it composes —
    // the gate, a rule, the handler — is built by that scope (messaging.md, the ambient unit of
    // work), so a Session written onto the scope its caller opened would reach a fresh provider
    // that never heard of it, and an elevated send (a background job's, the logo endpoint's, a
    // handler fetching the asset its own row names) would silently run as somebody else. The
    // instance field is what stays for a client host, where nothing opens a second scope and the
    // session is assigned once for the app's whole life.
    static readonly AsyncLocal<Session?> Flowing = new();

    Session? _assigned;

    // What the flow carried before this instance wrote to it, put back when the instance's scope
    // ends: an assignment belongs to the scope that made it and must not outlive it on the flow.
    Session? _previous;
    bool _wrote;

    // One instance, not a fresh one per read: a caller that never assigns a Session still edits
    // the one it is given in place (a page filling the active organization onto it), and a
    // getter that minted a new Session each time would swallow every such write.
    Session? _anonymous;

    protected Session? Assigned
    {
        get { return _assigned ?? Flowing.Value; }
    }

    public virtual Session Session
    {
        get { return Assigned ?? (_anonymous ??= new Session()); }
        set
        {
            if (!_wrote)
            {
                _previous = Flowing.Value;
                _wrote = true;
            }

            _assigned = value;
            Flowing.Value = value;
        }
    }

    public void Dispose()
    {
        if (_wrote && ReferenceEquals(Flowing.Value, _assigned))
        {
            Flowing.Value = _previous;
        }

        _wrote = false;
    }
}
