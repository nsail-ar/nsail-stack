// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Repositories;

// A commit status the harness left on a commit of the default branch: what it judged there
// (`harness/gate`, `harness/suite/{product}`, ...), whether it passed, and when. A Pending mark is a
// judgment under way on some harness: it is neither passed nor failed, and readers of a verdict skip it.
public sealed record StatusMark(string Sha, string Context, bool Ok, DateTimeOffset At, bool Pending = false)
{
    // The newest verdict of this context, newest commit first, and among one commit's the newest.
    public static StatusMark? Newest(IEnumerable<StatusMark> marks, string context, bool? ok = null)
    {
        return marks.Where(m => !m.Pending && m.Context == context && (ok is null || m.Ok == ok)).FirstOrDefault();
    }

    // The newest mark of this context, a pending one included.
    public static StatusMark? NewestOrPending(IEnumerable<StatusMark> marks, string context)
    {
        return marks.Where(m => m.Context == context).FirstOrDefault();
    }

    // When this context was last written, on whichever commit.
    public static DateTimeOffset? LastAt(IEnumerable<StatusMark> marks, string context)
    {
        return marks.Where(m => !m.Pending && m.Context == context).Select(m => (DateTimeOffset?)m.At).Max();
    }
}
