// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>A handler whose own send the gate passed marks the sends it derives: they, and
/// every send behind them, run without asking the caller's grants again, and the session stays
/// the caller's, so what they write still records who acted. Opened anywhere else — a page, a
/// background job, a test body with no gated send in its flow — it opens nothing and the send
/// is gated as the caller's (permissions.md, Evaluation).</summary>
public static class DelegatedSend
{
    // The flag rides the flow for the reason AmbientPublish does: the delegated send's own
    // nested sends cannot forget to pass it. It stops at a process boundary by construction —
    // an HttpSender's message arrives at the other side as an ordinary send and is gated there.
    static readonly AsyncLocal<bool> Inside = new();

    public static bool Active
    {
        get { return Inside.Value; }
    }

    public static Task Run(Func<Task> send)
    {
        return Run<object?>(async () =>
        {
            await send().ConfigureAwait(false);

            return null;
        });
    }

    // The boundary is the gate's own verdict on the flow: AmbientAuthorization holds one only
    // behind a send the gate passed, so a delegation cannot be opened where no caller was
    // answered. The flag is written, the send is STARTED, and the write is undone before this
    // method ever awaits — the same order of operations as AmbientPublish.Run.
    public static Task<TResult> Run<TResult>(Func<Task<TResult>> send)
    {
        if (!AmbientAuthorization.Answered)
        {
            return send();
        }

        var previous = Inside.Value;

        Inside.Value = true;

        try
        {
            return send();
        }
        finally
        {
            Inside.Value = previous;
        }
    }
}
