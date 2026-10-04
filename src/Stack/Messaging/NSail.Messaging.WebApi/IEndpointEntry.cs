// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.WebApi;

public interface IEndpointEntry
{
    void Configure(IEndpointRouteBuilder builder);
}
