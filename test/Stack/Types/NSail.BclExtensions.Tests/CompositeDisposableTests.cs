// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.BclExtensions;

namespace NSail.BclExtensions.Tests;

public sealed class CompositeDisposableTests
{
    [Fact]
    public void Constructor_CreatesEmptyCollection()
    {
        var exception = Record.Exception(() => new CompositeDisposable());

        Assert.Null(exception);
    }

    [Fact]
    public void Add_WithValidDisposable_AddsToCollection()
    {
        var composite = new CompositeDisposable();
        var called = false;
        var disposable = new DelegateDisposable(() => called = true);

        composite.Add(disposable);
        composite.Dispose();

        Assert.True(called);
    }

    [Fact]
    public void Add_WithNullDisposable_DoesNotThrow()
    {
        var composite = new CompositeDisposable();

        var exception = Record.Exception(() => composite.Add(null!));

        Assert.Null(exception);
    }

    [Fact]
    public void Add_WithNullDisposable_DoesNotAffectOtherDisposables()
    {
        var composite = new CompositeDisposable();
        var called = false;
        var disposable = new DelegateDisposable(() => called = true);

        composite.Add(disposable);
        composite.Add(null!);
        composite.Dispose();

        Assert.True(called);
    }

    [Fact]
    public void Dispose_CallsAllDisposables()
    {
        var composite = new CompositeDisposable();
        var call1 = false;
        var call2 = false;
        var call3 = false;

        composite.Add(new DelegateDisposable(() => call1 = true));
        composite.Add(new DelegateDisposable(() => call2 = true));
        composite.Add(new DelegateDisposable(() => call3 = true));

        composite.Dispose();

        Assert.True(call1);
        Assert.True(call2);
        Assert.True(call3);
    }

    [Fact]
    public void Dispose_WithEmptyCollection_DoesNotThrow()
    {
        var composite = new CompositeDisposable();

        var exception = Record.Exception(() => composite.Dispose());

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DisposesOnlyOnce()
    {
        var composite = new CompositeDisposable();
        var callCount = 0;

        composite.Add(new DelegateDisposable(() => callCount++));

        composite.Dispose();
        composite.Dispose();
        composite.Dispose();

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Dispose_WithThrowingDisposable_ContinuesDisposingOthers()
    {
        var composite = new CompositeDisposable();
        var call1 = false;
        var call2 = false;

        composite.Add(new DelegateDisposable(() => call1 = true));
        composite.Add(new DelegateDisposable(() => throw new InvalidOperationException()));
        composite.Add(new DelegateDisposable(() => call2 = true));

        composite.Dispose();

        Assert.True(call1);
        Assert.True(call2);
    }

    [Fact]
    public void Dispose_WithThrowingDisposable_DoesNotThrow()
    {
        var composite = new CompositeDisposable();

        composite.Add(new DelegateDisposable(() => throw new InvalidOperationException()));

        var exception = Record.Exception(() => composite.Dispose());

        Assert.Null(exception);
    }

    [Fact]
    public void Add_AfterDispose_DisposesImmediately()
    {
        var composite = new CompositeDisposable();
        composite.Dispose();

        var called = false;
        var disposable = new DelegateDisposable(() => called = true);

        composite.Add(disposable);

        Assert.True(called);
    }

    [Fact]
    public void Add_AfterDispose_DoesNotAffectPreviousDisposables()
    {
        var composite = new CompositeDisposable();
        var call1 = false;
        var call2 = false;

        composite.Add(new DelegateDisposable(() => call1 = true));
        composite.Dispose();

        Assert.True(call1);

        composite.Add(new DelegateDisposable(() => call2 = true));

        Assert.True(call2);
    }

    [Fact]
    public void Dispose_WithMultipleThrowingDisposables_SuppressesAllExceptions()
    {
        var composite = new CompositeDisposable();

        composite.Add(new DelegateDisposable(() => throw new InvalidOperationException("First")));
        composite.Add(new DelegateDisposable(() => throw new InvalidOperationException("Second")));
        composite.Add(new DelegateDisposable(() => throw new InvalidOperationException("Third")));

        var exception = Record.Exception(() => composite.Dispose());

        Assert.Null(exception);
    }

    [Fact]
    public void UsingStatement_DisposesAllItems()
    {
        var call1 = false;
        var call2 = false;

        using (var composite = new CompositeDisposable())
        {
            composite.Add(new DelegateDisposable(() => call1 = true));
            composite.Add(new DelegateDisposable(() => call2 = true));

            Assert.False(call1);
            Assert.False(call2);
        }

        Assert.True(call1);
        Assert.True(call2);
    }

    [Fact]
    public void ThreadSafety_AddAndDisposeFromMultipleThreads_DoesNotThrow()
    {
        var composite = new CompositeDisposable();
        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    composite.Add(new DelegateDisposable(() => { }));
                }
            }));
        }

        tasks.Add(Task.Run(() =>
        {
            Thread.Sleep(50);
            composite.Dispose();
        }));

        var exception = Record.Exception(() => Task.WaitAll(tasks.ToArray()));

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_CallsDisposablesInOrder()
    {
        var composite = new CompositeDisposable();
        var order = new List<int>();

        composite.Add(new DelegateDisposable(() => order.Add(1)));
        composite.Add(new DelegateDisposable(() => order.Add(2)));
        composite.Add(new DelegateDisposable(() => order.Add(3)));

        composite.Dispose();

        Assert.Equal(new[] { 1, 2, 3 }, order);
    }
}
