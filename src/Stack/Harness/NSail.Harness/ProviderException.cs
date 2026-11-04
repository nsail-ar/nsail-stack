// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// A board, a repository or a lock refused or failed a call: what a job may catch and carry on from.
// Status is the provider's own code when it has one (an HTTP status for a web API).
public class ProviderException(string message, int? status = null) : Exception(message)
{
    public int? Status { get; } = status;
}
