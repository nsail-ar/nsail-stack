// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using NSail.Problems;

namespace NSail.Components;

public sealed class Runner : IDisposable
{
    readonly IRunHost _host;
    CancellationTokenSource? _cancellationTokenSource;
    bool _disposed;

    public Runner(IRunHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    public bool IsRunning { get; private set; }

    public event Action<bool>? IsRunningChanged;

    public CancellationToken CancellationToken =>
        _cancellationTokenSource?.Token ?? CancellationToken.None;

    public Task Run(Func<Task> action, RunOptions options = RunOptions.None)
    {
        ArgumentNullException.ThrowIfNull(action);

        return Run(_ => action(), options);
    }

    public async Task Run(Func<CancellationToken, Task> action, RunOptions options = RunOptions.None)
    {
        ArgumentNullException.ThrowIfNull(action);

        // Nobody is left to run it for. A component's own end cancels whatever it had in flight
        // (Dispose), and the act that was in flight keeps going for one more line after that —
        // NsForm's post-save refetch is the one that bites — so a run asked for here gets the
        // same answer a cancelled one already gets below, for the same reason: there is no
        // screen left to put the result on, and no Problem anybody could read.
        // Thrown instead, it escapes the continuation into the nearest ErrorBoundary and the
        // person reads an error page about a screen that is already gone: nsail#1899, where a
        // session cut while somebody was saving Mi Perfil left them on a broken Mi Perfil
        // instead of at the door the refusal was taking them to.
        if (_disposed)
        {
            return;
        }

        if (IsRunning)
        {
            if (options.HasFlag(RunOptions.Replace))
            {
                Cancel();
            }
            else
            {
                ThrowIfRunning();
            }
        }

        using var cancellationTokenSource = new CancellationTokenSource();

        _cancellationTokenSource = cancellationTokenSource;

        var affectsSurface = !options.HasFlag(RunOptions.Background);

        try
        {
            SetRunning(true);

            if (affectsSurface)
            {
                _host.Surface?.Enter();
            }

            await action(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (BusinessException ex)
        {
            var problem = ex.ToProblem();
            var args = new ProblemEventArgs(problem, _host.Source, ex);

            await _host.Report(args);
        }
        catch (Exception ex)
        {
            var problem = SystemProblem.Unhandled();
            var args = new ProblemEventArgs(problem, _host.Source, ex);

            await _host.Report(args);
        }
        finally
        {
            if (affectsSurface)
            {
                _host.Surface?.Exit();
            }

            if (ReferenceEquals(_cancellationTokenSource, cancellationTokenSource))
            {
                _cancellationTokenSource = null;
                SetRunning(false);
            }
        }
    }

    public Task Raise<TArgs>(EventCallback<TArgs> callback, TArgs args, RunOptions options = RunOptions.None)
        where TArgs : AsyncEventArgs
    {
        ArgumentNullException.ThrowIfNull(args);

        return Run(async cancellationToken =>
        {
            args.CancellationToken = cancellationToken;

            if (callback.HasDelegate)
            {
                await callback.InvokeAsync(args);
            }

            if (args.Problem is not null)
            {
                await _host.Report(new ProblemEventArgs(args.Problem, _host.Source, null));
            }
        }, options);
    }

    public void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    void SetRunning(bool value)
    {
        if (IsRunning == value)
        {
            return;
        }

        IsRunning = value;
        IsRunningChanged?.Invoke(value);
        _host.StateChanged();
    }

    void ThrowIfRunning()
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Runner is already running.");
        }
    }
}
