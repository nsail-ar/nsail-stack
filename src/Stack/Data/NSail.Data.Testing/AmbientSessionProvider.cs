// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Security;

namespace NSail.Data.Testing;

// InProcessSender gives an operation its own service scope, so a Session written onto the
// scope a test opened is gone the moment the send starts — the gate would meet an anonymous
// provider and answer Forbidden. A host does not have this problem because its provider
// serves the request's principal, which no scope owns; this is the same shape for a test,
// backed by the async flow the send already runs in.
sealed class AmbientSessionProvider : SessionProvider
{
    static readonly AsyncLocal<Session?> Current = new();

    public override Session Session
    {
        get { return Current.Value ?? new Session(); }
        set { Current.Value = value; }
    }
}
