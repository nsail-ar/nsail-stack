// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging.Runtime.Pipelines;
using NSail.Messaging.Runtime.Sending;
using NSail.Messaging.Runtime.UnitOfWork;
using NSail.Problems;

namespace NSail.Messaging.Runtime.Tests;

// The ambient unit of work, at the altitude where its rules are rules: the outermost in-process
// send owns the scope and the transaction, a nested send joins both, anything escaping the
// outermost undoes everything, and a fork inside one operation is refused by name. The five
// business faces this cures are pinned in NSail.Optical.Scenarios.Tests.
public sealed class AmbientUnitOfWorkTests
{
    [Fact]
    public async Task A_nested_send_runs_in_the_outermost_sends_scope()
    {
        var services = Build();

        var log = services.GetRequiredService<Log>();

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Mediator>().Send(new Outer());
        }

        Assert.Equal(2, log.Scopes.Count);
        Assert.Single(log.Scopes.Distinct());
    }

    [Fact]
    public async Task Two_operations_do_not_share_a_scope()
    {
        var services = Build();

        var log = services.GetRequiredService<Log>();

        await using (var scope = services.CreateAsyncScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<Mediator>();

            await mediator.Send(new Inner());
            await mediator.Send(new Inner());
        }

        Assert.Equal(2, log.Scopes.Distinct().Count());
    }

    [Fact]
    public async Task One_operation_opens_and_commits_exactly_one_unit()
    {
        var services = Build();

        var log = services.GetRequiredService<Log>();

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Mediator>().Send(new Outer());
        }

        Assert.Equal(["begin", "commit"], log.Steps);
    }

    [Fact]
    public async Task Anything_escaping_the_outermost_send_undoes_the_whole_operation()
    {
        var services = Build();

        var log = services.GetRequiredService<Log>();

        await using (var scope = services.CreateAsyncScope())
        {
            await Assert.ThrowsAsync<BusinessException>(
                () => scope.ServiceProvider.GetRequiredService<Mediator>().Send(new Failing()));
        }

        Assert.Equal(["begin", "rollback"], log.Steps);
    }

    [Fact]
    public async Task Two_sends_started_in_parallel_inside_one_operation_are_refused_by_name()
    {
        var services = Build();

        var log = services.GetRequiredService<Log>();

        await using (var scope = services.CreateAsyncScope())
        {
            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => scope.ServiceProvider.GetRequiredService<Mediator>().Send(new Forking()));

            Assert.Equal("ConcurrentSend", refusal.Code);
            Assert.Equal(nameof(Inner), Assert.Single(refusal.Issues).Source);
        }

        Assert.Equal(["begin", "rollback"], log.Steps);
    }

    [Fact]
    public async Task A_send_on_another_transport_opens_no_unit()
    {
        var services = Build();

        var log = services.GetRequiredService<Log>();

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Mediator>().Send(new Remote());
        }

        Assert.Empty(log.Steps);
    }

    [Fact]
    public async Task The_outermost_sends_interceptor_is_built_by_the_sends_own_scope()
    {
        var services = Build();

        var log = services.GetRequiredService<Log>();

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Mediator>().Send(new Watched());
        }

        Assert.Equal(2, log.Scopes.Count);
        Assert.Single(log.Scopes.Distinct());
    }

    [Fact]
    public async Task A_refusal_undoes_the_outermost_interceptors_own_write()
    {
        var services = Build();

        var log = services.GetRequiredService<Log>();
        var ledger = services.GetRequiredService<Ledger>();

        await using (var scope = services.CreateAsyncScope())
        {
            await Assert.ThrowsAsync<BusinessException>(
                () => scope.ServiceProvider.GetRequiredService<Mediator>().Send(new Watched { Refuse = true }));
        }

        Assert.Empty(ledger.Entries);
        Assert.Equal(["begin", "rollback"], log.Steps);
    }

    [Fact]
    public async Task A_refusal_undoes_the_outermost_interceptors_own_write_when_the_message_carries_a_result()
    {
        var services = Build();

        var ledger = services.GetRequiredService<Ledger>();

        await using (var scope = services.CreateAsyncScope())
        {
            await Assert.ThrowsAsync<BusinessException>(
                () => scope.ServiceProvider.GetRequiredService<Mediator>().Send(new Counted { Refuse = true }));
        }

        Assert.Empty(ledger.Entries);
    }

    [Fact]
    public async Task A_successful_operation_commits_the_interceptors_write_with_the_handlers()
    {
        var services = Build();

        var log = services.GetRequiredService<Log>();
        var ledger = services.GetRequiredService<Ledger>();

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Mediator>().Send(new Watched());
        }

        Assert.Equal(["interceptor", "handler"], ledger.Entries);
        Assert.Equal(["begin", "commit"], log.Steps);
    }

    // The generated endpoints' own entry pipeline, resolved per request by [FromServices]. It is
    // the top-level send of every HTTP call a product makes, so the rule above has to hold from
    // here too and not only from Mediator.Send.
    [Fact]
    public async Task A_refusal_undoes_the_interceptors_write_from_the_endpoints_entry_pipeline()
    {
        var services = Build();

        var ledger = services.GetRequiredService<Ledger>();

        await using (var scope = services.CreateAsyncScope())
        {
            var pipeline = scope.ServiceProvider.GetRequiredService<InProcessSendPipeline<Watched>>();

            await Assert.ThrowsAsync<BusinessException>(
                () => pipeline.Execute(new Watched { Refuse = true }, null, CancellationToken.None));
        }

        Assert.Empty(ledger.Entries);
    }

    // A send crossing a process boundary is a second operation by design: no unit opens, and its
    // interceptor is built by the caller's scope because there is no other scope to build it from.
    [Fact]
    public async Task A_transport_sends_interceptor_stays_in_the_callers_scope()
    {
        var services = Build();

        var log = services.GetRequiredService<Log>();
        var ledger = services.GetRequiredService<Ledger>();

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Mediator>().Send(new Remote());

            Assert.Equal(scope.ServiceProvider.GetRequiredService<ScopeMarker>().Id, Assert.Single(log.Scopes));
        }

        Assert.Empty(log.Steps);
        Assert.Equal(["interceptor"], ledger.Entries);
    }

    static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddMessaging();

        services.AddSingleton<Log>();
        services.AddSingleton<Ledger>();
        services.AddScoped<ScopeMarker>();
        services.AddScoped<Book>();
        services.AddScoped<IUnitOfWork, RecordingUnitOfWork>();

        services.AddScoped<ISender<Outer>, InProcessSender<Outer>>();
        services.AddScoped<ISender<Inner>, InProcessSender<Inner>>();
        services.AddScoped<ISender<Failing>, InProcessSender<Failing>>();
        services.AddScoped<ISender<Forking>, InProcessSender<Forking>>();
        services.AddScoped<ISender<Watched>, InProcessSender<Watched>>();
        services.AddScoped<ISender<Counted, int>, InProcessSender<Counted, int>>();
        services.AddScoped<ISender<Remote>, RemoteSender>();

        services.AddScoped<IHandler<Outer>, OuterHandler>();
        services.AddScoped<IHandler<Inner>, InnerHandler>();
        services.AddScoped<IHandler<Failing>, FailingHandler>();
        services.AddScoped<IHandler<Forking>, ForkingHandler>();
        services.AddScoped<IHandler<Watched>, WatchedHandler>();
        services.AddScoped<IHandler<Counted, int>, CountedHandler>();

        services.AddScoped<IInterceptor<Watched>, WatchingInterceptor>();
        services.AddScoped<IInterceptor<Counted, int>, CountingInterceptor>();
        services.AddScoped<IInterceptor<Remote>, RemoteInterceptor>();

        return services.BuildServiceProvider();
    }

    sealed record Outer : IMessage;

    sealed record Inner : IMessage;

    sealed record Failing : IMessage;

    sealed record Forking : IMessage;

    sealed record Remote : IMessage;

    sealed record Watched : IMessage
    {
        public bool Refuse { get; init; }
    }

    sealed record Counted : IMessage<int>
    {
        public bool Refuse { get; init; }
    }

    sealed class Log
    {
        public List<Guid> Scopes { get; } = [];

        public List<string> Steps { get; } = [];

        public void Add(Guid scope)
        {
            lock (Scopes)
            {
                Scopes.Add(scope);
            }
        }

        public void Step(string step)
        {
            lock (Steps)
            {
                Steps.Add(step);
            }
        }
    }

    sealed class ScopeMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    // The store everything durable ends up in — the database, at this altitude.
    sealed class Ledger
    {
        public List<string> Entries { get; } = [];

        public void Add(string entry)
        {
            lock (Entries)
            {
                Entries.Add(entry);
            }
        }

        public void Remove(IEnumerable<string> entries)
        {
            lock (Entries)
            {
                foreach (var entry in entries)
                {
                    Entries.Remove(entry);
                }
            }
        }
    }

    // A write reaches the ledger at once and is undone by the scope's own unit — the shape a
    // DbContext has inside a transaction, where SaveChanges is visible immediately and durable
    // only at the commit. A write made through some other scope's book is nobody's to undo.
    sealed class Book
    {
        readonly Ledger _ledger;
        readonly List<string> _written = [];

        public Book(Ledger ledger)
        {
            _ledger = ledger;
        }

        public void Write(string entry)
        {
            _written.Add(entry);
            _ledger.Add(entry);
        }

        public void Keep()
        {
            _written.Clear();
        }

        public void Undo()
        {
            _ledger.Remove(_written);
            _written.Clear();
        }
    }

    sealed class RecordingUnitOfWork : IUnitOfWork
    {
        readonly Log _log;
        readonly Book _book;

        public RecordingUnitOfWork(Log log, Book book)
        {
            _log = log;
            _book = book;
        }

        public Task Begin(CancellationToken cancellationToken)
        {
            _log.Step("begin");
            return Task.CompletedTask;
        }

        public Task Commit(CancellationToken cancellationToken)
        {
            _log.Step("commit");
            _book.Keep();
            return Task.CompletedTask;
        }

        public Task Rollback()
        {
            _log.Step("rollback");
            _book.Undo();
            return Task.CompletedTask;
        }
    }

    sealed class OuterHandler : IHandler<Outer>
    {
        readonly Mediator _mediator;
        readonly ScopeMarker _marker;
        readonly Log _log;

        public OuterHandler(Mediator mediator, ScopeMarker marker, Log log)
        {
            _mediator = mediator;
            _marker = marker;
            _log = log;
        }

        public async Task Handle(Outer message, CancellationToken cancellationToken = default)
        {
            _log.Add(_marker.Id);

            await _mediator.Send(new Inner(), cancellationToken);
        }
    }

    sealed class InnerHandler : IHandler<Inner>
    {
        readonly ScopeMarker _marker;
        readonly Log _log;

        public InnerHandler(ScopeMarker marker, Log log)
        {
            _marker = marker;
            _log = log;
        }

        public async Task Handle(Inner message, CancellationToken cancellationToken = default)
        {
            // Long enough that two of these really overlap when a handler starts both before
            // awaiting either — the fork the guard exists for.
            await Task.Delay(30, cancellationToken);

            _log.Add(_marker.Id);
        }
    }

    sealed class FailingHandler : IHandler<Failing>
    {
        readonly Mediator _mediator;

        public FailingHandler(Mediator mediator)
        {
            _mediator = mediator;
        }

        public async Task Handle(Failing message, CancellationToken cancellationToken = default)
        {
            await _mediator.Send(new Inner(), cancellationToken);

            throw new BusinessException(BusinessProblem.RuleViolation("Refused", "The step after the nested send says no."));
        }
    }

    sealed class ForkingHandler : IHandler<Forking>
    {
        readonly Mediator _mediator;

        public ForkingHandler(Mediator mediator)
        {
            _mediator = mediator;
        }

        public Task Handle(Forking message, CancellationToken cancellationToken = default)
        {
            return Task.WhenAll(
                _mediator.Send(new Inner(), cancellationToken),
                _mediator.Send(new Inner(), cancellationToken));
        }
    }

    sealed class WatchedHandler : IHandler<Watched>
    {
        readonly ScopeMarker _marker;
        readonly Book _book;
        readonly Log _log;

        public WatchedHandler(ScopeMarker marker, Book book, Log log)
        {
            _marker = marker;
            _book = book;
            _log = log;
        }

        public Task Handle(Watched message, CancellationToken cancellationToken = default)
        {
            _log.Add(_marker.Id);
            _book.Write("handler");

            if (message.Refuse)
                throw new BusinessException(BusinessProblem.RuleViolation("Refused", "The handler says no after the interceptor already wrote."));

            return Task.CompletedTask;
        }
    }

    sealed class CountedHandler : IHandler<Counted, int>
    {
        readonly Book _book;

        public CountedHandler(Book book)
        {
            _book = book;
        }

        public Task<int> Handle(Counted message, CancellationToken cancellationToken = default)
        {
            _book.Write("handler");

            if (message.Refuse)
                throw new BusinessException(BusinessProblem.RuleViolation("Refused", "The handler says no after the interceptor already wrote."));

            return Task.FromResult(1);
        }
    }

    // Writes before next() so the handler's refusal comes after it: what the interceptor left
    // behind is the whole question. CustomerInterceptor writes after next() instead, which is
    // the same seam read from the other side — either way the write belongs to the operation.
    sealed class WatchingInterceptor : IInterceptor<Watched>
    {
        readonly ScopeMarker _marker;
        readonly Book _book;
        readonly Log _log;

        public WatchingInterceptor(ScopeMarker marker, Book book, Log log)
        {
            _marker = marker;
            _book = book;
            _log = log;
        }

        public Task Invoke(Watched message, PipelineDelegate<Watched> next, CancellationToken cancellationToken)
        {
            _log.Add(_marker.Id);
            _book.Write("interceptor");

            return next(message, cancellationToken);
        }
    }

    sealed class CountingInterceptor : IInterceptor<Counted, int>
    {
        readonly Book _book;

        public CountingInterceptor(Book book)
        {
            _book = book;
        }

        public Task<int> Invoke(Counted message, PipelineDelegate<Counted, int> next, CancellationToken cancellationToken)
        {
            _book.Write("interceptor");

            return next(message, cancellationToken);
        }
    }

    sealed class RemoteInterceptor : IInterceptor<Remote>
    {
        readonly ScopeMarker _marker;
        readonly Book _book;
        readonly Log _log;

        public RemoteInterceptor(ScopeMarker marker, Book book, Log log)
        {
            _marker = marker;
            _book = book;
            _log = log;
        }

        public Task Invoke(Remote message, PipelineDelegate<Remote> next, CancellationToken cancellationToken)
        {
            _log.Add(_marker.Id);
            _book.Write("interceptor");

            return next(message, cancellationToken);
        }
    }

    sealed class RemoteSender : ISender<Remote>
    {
        public Task Send(Remote message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
