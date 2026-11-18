// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// A call whose connection failed before or while its answer arrived: the provider's failure, as a
// timeout is, never a break. It has no status, as nothing answered whole.
public sealed class ProviderTransportException(string message, Exception inner) : ProviderException(message, inner: inner);
