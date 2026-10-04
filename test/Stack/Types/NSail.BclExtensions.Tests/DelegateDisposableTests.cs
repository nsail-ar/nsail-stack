// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.BclExtensions;

namespace NSail.BclExtensions.Tests;

public sealed class DelegateDisposableTests
{
    [Fact]
    public void Constructor_WithNullAction_ThrowsArgumentNullException()
    {
        Action? action = null;

        var exception = Assert.Throws<ArgumentNullException>(() => new DelegateDisposable(action!));

        Assert.NotNull(exception);
    }

    [Fact]
    public void Constructor_WithValidAction_DoesNotThrow()
    {
        var exception = Record.Exception(() => new DelegateDisposable(() => { }));

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_CallsAction()
    {
        var called = false;
        var disposable = new DelegateDisposable(() => called = true);

        disposable.Dispose();

        Assert.True(called);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_CallsActionOnlyOnce()
    {
        var callCount = 0;
        var disposable = new DelegateDisposable(() => callCount++);

        disposable.Dispose();
        disposable.Dispose();
        disposable.Dispose();

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Dispose_WithActionThatThrows_PropagatesException()
    {
        var disposable = new DelegateDisposable(() => throw new InvalidOperationException("Test exception"));

        var exception = Assert.Throws<InvalidOperationException>(() => disposable.Dispose());

        Assert.Equal("Test exception", exception.Message);
    }

    [Fact]
    public void Dispose_AfterAlreadyDisposed_DoesNotCallActionAgain()
    {
        var callCount = 0;
        var disposable = new DelegateDisposable(() => callCount++);

        disposable.Dispose();
        var firstCallCount = callCount;
        disposable.Dispose();

        Assert.Equal(1, firstCallCount);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Dispose_WithActionThatModifiesState_ModifiesStateOnce()
    {
        var state = 0;
        var disposable = new DelegateDisposable(() => state = 42);

        disposable.Dispose();
        disposable.Dispose();

        Assert.Equal(42, state);
    }

    [Fact]
    public void UsingStatement_CallsDisposeAutomatically()
    {
        var called = false;

        using (var disposable = new DelegateDisposable(() => called = true))
        {
            Assert.False(called);
        }

        Assert.True(called);
    }

    [Fact]
    public void UsingDeclaration_CallsDisposeAtEndOfScope()
    {
        var called = false;
        
        void TestScope()
        {
            using var disposable = new DelegateDisposable(() => called = true);
            Assert.False(called);
        }

        TestScope();
        Assert.True(called);
    }
}
