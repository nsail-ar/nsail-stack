// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>Scoped source of the current Session. The default is anonymous — Iam
/// replaces it server-side with one built from the request's principal; jobs set
/// Session.System(); tests set whatever they need.</summary>
public class SessionProvider
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

    // One instance, not a fresh one per read: a caller that never assigns a Session still edits
    // the one it is given in place (a page filling the active organization onto it), and a
    // getter that minted a new Session each time would swallow every such write.
    Session? _anonymous;

    protected Session? Assigned
    {
        get { return Flowing.Value ?? _assigned; }
    }

    public virtual Session Session
    {
        get { return Assigned ?? (_anonymous ??= new Session()); }
        set
        {
            _assigned = value;
            Flowing.Value = value;
        }
    }
}
