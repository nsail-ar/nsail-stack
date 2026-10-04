// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;

namespace NSail.Components;

public abstract class NsComponent : ComponentBase, IRunHost, IDisposable
{
    List<IDisposable>? _disposables;

    [CascadingParameter]
    public SurfaceContext? Surface { get; set; }

    [Parameter]
    public EventCallback<ProblemEventArgs> OnProblem { get; set; }

    protected Runner Runner => field ??= new(this);

    protected bool IsRunning => Runner.IsRunning;

    protected CancellationToken CancellationToken => Runner.CancellationToken;

    protected Task Run(Func<Task> action, RunOptions options = RunOptions.None)
    {
        return Runner.Run(action, options);
    }

    protected Task Run(Func<CancellationToken, Task> action, RunOptions options = RunOptions.None)
    {
        return Runner.Run(action, options);
    }

    protected Task Raise<TArgs>(EventCallback<TArgs> callback, TArgs args, RunOptions options = RunOptions.None)
        where TArgs : AsyncEventArgs
    {
        return Runner.Raise(callback, args, options);
    }

    protected void CancelWork()
    {
        Runner.Cancel();
    }

    /// <summary>Ties a disposable (e.g. a subscription) to this component's lifetime.</summary>
    protected T Using<T>(T disposable) where T : IDisposable
    {
        ArgumentNullException.ThrowIfNull(disposable);

        (_disposables ??= []).Add(disposable);

        return disposable;
    }

    protected void Using(params IDisposable[] disposables)
    {
        foreach (var disposable in disposables)
        {
            Using<IDisposable>(disposable);
        }
    }

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

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // A surface outlives the pages rendered in it — only named ones remount per route —
            // so a component that reported itself dirty and then unmounted left that report
            // behind forever: every later screen on the same surface offered an enabled submit
            // and asked about changes nobody had made (Leonardo, 2026-08-10, "smtp siempre me
            // pregunta antes de navegar aunque no haya tocado nada"). SetDirty(this) is keyed on
            // the instance, so the instance's own end is where it is spent — no editor has to
            // remember, and none of them could remember for the case that leaks: leaving the
            // page with the change still unsaved. Retired rather than only spent: a gesture still
            // in flight reports itself when it lands, after this instance is gone, and nothing
            // would ever spend that one.
            Surface?.Retire(this);

            if (_disposables is not null)
            {
                for (var i = _disposables.Count - 1; i >= 0; i--)
                {
                    _disposables[i].Dispose();
                }

                _disposables = null;
            }

            Runner.Dispose();
        }
    }
}
