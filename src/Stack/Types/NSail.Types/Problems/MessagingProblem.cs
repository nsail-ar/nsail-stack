// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Problems;

public static class MessagingProblem
{
    public static Problem HandlerNotFound(string messageType)
    {
        return new(
            code: "HandlerNotFound",
            title: "The operation could not be processed",
            issues: new[]
            {
                new Issue(
                    code: "HandlerNotFound",
                    message: $"No handler is registered for message type {messageType}",
                    source: messageType)
            },
            status: 500);
    }

    public static Problem HandlerNotFound<TMessage>()
    {
        return HandlerNotFound(typeof(TMessage).Name);
    }

    public static Problem SenderNotFound(string messageType)
    {
        return new(
            code: "SenderNotFound",
            title: "The operation could not be processed",
            issues: new[]
            {
                new Issue(
                    code: "SenderNotFound",
                    message: $"No sender is registered for message type {messageType}",
                    source: messageType)
            },
            status: 500);
    }

    public static Problem SenderNotFound<TMessage>()
    {
        return SenderNotFound(typeof(TMessage).Name);
    }

    public static Problem ClientNotConfigured(string clientName, string messageType)
    {
        return new(
            code: "ClientNotConfigured",
            title: "The operation could not be processed",
            issues: new[]
            {
                new Issue(
                    code: "ClientNotConfigured",
                    message: $"The HTTP client '{clientName}' has no base address, so {messageType} cannot be sent. Configure 'HttpClients:{clientName}:BaseUrl'.",
                    source: clientName)
            },
            status: 500);
    }

    public static Problem MultipleHandlers(string messageType)
    {
        return new(
            code: "MultipleHandlers",
            title: "The operation could not be processed",
            issues: new[]
            {
                new Issue(
                    code: "MultipleHandlers",
                    message: $"Multiple handlers are registered for message type '{messageType}'",
                    source: messageType)
            },
            status: 500);
    }

    public static Problem MultipleHandlers<TMessage>()
    {
        return MultipleHandlers(typeof(TMessage).Name);
    }

    public static Problem MultipleSenders<TMessage>()
    {
        return MultipleSenders(typeof(TMessage).Name);
    }

    public static Problem MultipleSenders(string messageType)
    {
        return new(
            code: "MultipleSenders",
            title: "The operation could not be processed",
            issues: new[]
            {
                new Issue(
                    code: "MultipleSenders",
                    message: $"Multiple senders are registered for message type '{messageType}'",
                    source: messageType)
            },
            status: 500);
    }

    public static Problem ConcurrentSend(string messageType)
    {
        return new(
            code: "ConcurrentSend",
            title: "The operation could not be processed",
            issues: new[]
            {
                new Issue(
                    code: "ConcurrentSend",
                    message: $"{messageType} was sent while another send of the same operation was still running. Sends nested in one operation share its scope and its transaction, so they run one after another.",
                    source: messageType)
            },
            status: 500);
    }

    public static Problem Timeout(string messageType)
    {
        return new(
            code: "Timeout",
            title: "The operation timed out",
            issues: new[]
            {
                new Issue(
                    code: "Timeout",
                    message: $"Timeout while executing {messageType}",
                    source: messageType)
            },
            status: 504);
    }
}
