// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.Security;
using System.Security.Authentication;
using NSail.Problems;

namespace NSail.Messaging.WebApi;

public sealed class ErrorMiddleware : IMiddleware
{
    readonly ILogger<ErrorMiddleware> _logger;

    public ErrorMiddleware(ILogger<ErrorMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            var mapped = Map(exception, context);
            var problem = mapped ?? SystemProblem.Unhandled();

            Log(exception, mapped, problem, context);

            context.Response.Clear();
            context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

            var document = context.Request.PrefersDocument()
                ? context.RequestServices.GetService<IProblemDocumentProvider>()
                : null;

            if (document is not null && await Draw(document, context, problem))
            {
                return;
            }

            await context.Response.WriteAsJsonAsync(problem, cancellationToken: context.RequestAborted);
        }
    }

    // Mapping keeps the response a client sees; the level is read off what the exception is. A
    // user-caused outcome is one Warning line. A bug-class exception wearing a 404 or 403, a
    // timeout and any 5xx are system distress: the exception with its stack goes to Error (a
    // timeout stays Warning). A cancellation from the request's own token is the caller leaving
    // and stays quiet; any other is a vendor timeout and is logged.
    void Log(Exception exception, Problem? mapped, Problem problem, HttpContext context)
    {
        if (mapped is null)
        {
            _logger.LogError(exception, "Unhandled exception mapped to problem {Code}", problem.Code);

            return;
        }

        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        if (exception is BusinessException && problem.Status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Exception mapped to problem {Code}: {Message}. Issues: {Issues}",
                problem.Code,
                exception.Message,
                string.Join("; ", problem.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        }
        else if (exception is KeyNotFoundException or UnauthorizedAccessException)
        {
            _logger.LogError(
                exception,
                "Exception mapped to problem {Code}: {Message}",
                problem.Code,
                exception.Message);
        }
        else if (exception is TimeoutException or OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Exception mapped to problem {Code}: {Message}",
                problem.Code,
                exception.Message);
        }
        else
        {
            _logger.LogWarning(
                "Exception mapped to problem {Code}: {Message}",
                problem.Code,
                exception.Message);
        }
    }

    // The document is a render on a request that already died once, so it is the one write here
    // that can fail on its own — and a failure that left this handler would arrive as the
    // request's fault and bury the exception just logged above. That is how a page whose
    // prerender threw answered with nothing but "'RemoteNavigationManager' already initialized"
    // (nsail#1837): the real cause was reported and then overwritten. So the page gets its own
    // line and the caller gets the machine's answer, which needs no renderer.
    async Task<bool> Draw(IProblemDocumentProvider document, HttpContext context, Problem problem)
    {
        try
        {
            await document.Write(context, problem);

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "The error page could not be rendered for problem {Code}", problem.Code);

            // Half a page is still a page as far as the socket is concerned: once bytes have
            // left, the JSON below would append to them. Nothing more can be said to this
            // caller, and the line above is what says it to us.
            if (context.Response.HasStarted)
            {
                return true;
            }

            // What the failed render managed to set — a content type, a header of its own —
            // goes with it, so the machine's answer is the same bytes it would have been.
            context.Response.Clear();
            context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

            return false;
        }
    }

    // Null means unhandled, and the log level is read off that: a second list of exception types
    // to decide Warning versus Error would be free to drift from this one.
    private static Problem? Map(Exception exception, HttpContext context)
    {
        return exception switch
        {
            BusinessException businessException => businessException.ToProblem(),
            AuthenticationException => SecurityProblem.Unauthorized(),
            UnauthorizedAccessException or SecurityException => SecurityProblem.Forbidden(GetRequestTarget(context)),
            ValidationException validationException => InputProblem.InvalidModel(
                new Issue(
                    code: "Invalid",
                    message: validationException.Message,
                    source: validationException.ValidationAttribute?.TypeId?.ToString())),
            BadHttpRequestException badHttpRequestException => InputProblem.InvalidModel(
                new Issue(
                    code: "BadRequest",
                    message: badHttpRequestException.Message,
                    source: GetRequestTarget(context))),
            KeyNotFoundException => BusinessProblem.NotFound("Common.Resource", GetRequestTarget(context)),
            TimeoutException => ExternalProblem.Timeout("Dependency"),
            OperationCanceledException => SystemProblem.Canceled(),
            _ => null
        };
    }

    private static string GetRequestTarget(HttpContext context)
    {
        return context.Request.Path.HasValue
            ? context.Request.Path.Value!
            : "resource";
    }
}
