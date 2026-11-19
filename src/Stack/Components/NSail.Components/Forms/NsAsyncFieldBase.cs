// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace NSail.Components;

public abstract class NsAsyncFieldBase<TValue> : NsFieldBase<TValue>, IRunHost
{
    [CascadingParameter]
    public SurfaceContext? Surface { get; set; }

    [Parameter]
    public EventCallback<ProblemEventArgs> OnProblem { get; set; }

    [Inject]
    ILogger<Runner> RunLog { get; init; } = default!;

    protected Runner Runner => field ??= new(this, RunLog);

    object IRunHost.Source => this;

    void IRunHost.StateChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    public virtual async Task Report(ProblemEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (OnProblem.HasDelegate)
        {
            await OnProblem.InvokeAsync(args);
        }

        if (args.Handled)
        {
            return;
        }

        if (Surface is not null)
        {
            await Surface.Report(args);
        }
    }

    protected override void DisposeCore()
    {
        Runner.Dispose();
        base.DisposeCore();
    }
}
