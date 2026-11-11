// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// A call the provider did not answer within its client's timeout: its failure, as a 5xx is, never
// the host's stop. It has no status, as nothing answered.
public sealed class ProviderTimeoutException(string message, Exception inner) : ProviderException(message, inner: inner);
