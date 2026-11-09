// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSail.Messaging.WebApi;
using NSail.Problems;

namespace NSail.BaseServices.WebApi.Tests;

public sealed class ErrorMiddlewareTests
{
    [Fact]
    public async Task A_bug_class_exception_keeps_its_response_and_logs_its_stack_at_error()
    {
        var (context, logger) = await Run(new KeyNotFoundException("missing"));
        Assert.Equal(404, context.Response.StatusCode);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<KeyNotFoundException>(entry.Exception);

        (context, logger) = await Run(new UnauthorizedAccessException("denied"));
        Assert.Equal(403, context.Response.StatusCode);
        entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<UnauthorizedAccessException>(entry.Exception);
    }

    [Fact]
    public async Task A_cancellation_from_the_request_token_is_quiet_and_a_vendor_one_is_logged()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var (_, quiet) = await Run(new TaskCanceledException(), source.Token);
        Assert.Empty(quiet.Entries);

        var (context, loud) = await Run(new TaskCanceledException("vendor timeout"));
        Assert.Equal(LogLevel.Warning, Assert.Single(loud.Entries).Level);
        Assert.Equal(500, context.Response.StatusCode);
    }

    [Fact]
    public async Task A_user_caused_outcome_is_one_warning_without_a_stack()
    {
        var (_, logger) = await Run(new BadHttpRequestException("bad"));
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Null(entry.Exception);
    }

    static async Task<(DefaultHttpContext, Capture)> Run(Exception thrown, CancellationToken aborted = default)
    {
        var logger = new Capture();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestAborted = aborted;
        var middleware = new ErrorMiddleware(logger);

        try
        {
            await middleware.InvokeAsync(context, _ => throw thrown);
        }
        catch (OperationCanceledException) when (aborted.IsCancellationRequested)
        {
            // The body write honours the aborted token, as it does for a caller that left.
        }

        return (context, logger);
    }

    sealed class Capture : ILogger<ErrorMiddleware>
    {
        public List<(LogLevel Level, Exception? Exception, string Text)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, exception, formatter(state, exception)));
        }
    }
}
