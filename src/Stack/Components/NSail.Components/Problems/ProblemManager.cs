// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Localization;
using NSail.Problems;

namespace NSail.Components;

public class ProblemManager(DialogManager dialogs, StringManager strings)
{
    public virtual Task Report(ProblemEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Handled)
        {
            return Task.CompletedTask;
        }

        Report(args.Problem);
        args.Handled = true;

        return Task.CompletedTask;
    }

    public virtual void Report(Problem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        dialogs.Notify(GetMessage(problem), GetSeverity(problem));
    }

    /// <summary>The issues carry the reason ("delete the child organizations first"); the
    /// title only names the failure. Falls back to the title when there are none. Public so a
    /// surface that draws a refusal itself (NsLoad) asks the same source for the same words.</summary>
    public virtual string GetMessage(Problem problem)
    {
        var messages = problem.Issues
            .Select(strings.Translate)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList();

        if (messages.Count == 0)
        {
            return strings.Translate(problem);
        }

        return string.Join(" ", messages);
    }

    protected virtual NsSeverity GetSeverity(Problem problem)
    {
        return NsSeverity.Error;
    }
}
