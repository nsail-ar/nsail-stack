// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>What the gate said about the send this flow is running, for the plumbing that runs
/// behind it and acts on it — the org filter, which takes the organization a message names only
/// where a policy looked at it (<see cref="AuthorizationResult.FieldsVetted"/>) and narrows to
/// the caller's own branch unless the grant was authored across every one
/// (<see cref="AuthorizationResult.EveryOrganization"/>).
///
/// <para>With no gate behind it — a host that composes none, a reaction the gate exempts, a
/// session the gate waves through — the answers are the floor's: the values count as read,
/// because none of those is a caller passing a value nobody checked (the only thing that turns
/// it off is an allowance that ignores fields on purpose, which is
/// <see cref="DependencyPolicyHandler"/> and nothing else), and the branch still narrows,
/// because reading wide is something a grant has to say.</para></summary>
public static class AmbientAuthorization
{
    // It travels by AsyncLocal for the reason AmbientPublish does: the gate and the plumbing
    // behind it are separate interceptors, composed by separate hosts, so there is no argument
    // to thread and no instance both hold. A scoped instance would be the wrong holder besides —
    // one scope spans the whole operation, and a nested send's verdict would overwrite the
    // verdict of the send it is nested in.
    static readonly AsyncLocal<AmbientGrant?> Granted = new();

    public static bool FieldsVetted
    {
        get { return Granted.Value?.FieldsVetted ?? true; }
    }

    public static bool EveryOrganization
    {
        get { return Granted.Value?.EveryOrganization ?? false; }
    }

    internal static Task Vetting(AmbientGrant grant, Func<Task> send)
    {
        return Vetting<object?>(grant, async () =>
        {
            await send().ConfigureAwait(false);

            return null;
        });
    }

    // The value is written, the send is STARTED, and the write is undone before this method ever
    // awaits: the flow that was started captured it, and the caller — which has not awaited yet —
    // must not keep it. Same order of operations as AmbientPublish.Run, for the same reason.
    internal static Task<TResult> Vetting<TResult>(AmbientGrant grant, Func<Task<TResult>> send)
    {
        var previous = Granted.Value;

        Granted.Value = grant;

        try
        {
            return send();
        }
        finally
        {
            Granted.Value = previous;
        }
    }
}

// The verdict's two facts, together: one holder, so a send that carries one carries the other
// and neither can be left over from the send it is nested in.
readonly record struct AmbientGrant(bool FieldsVetted, bool EveryOrganization);
