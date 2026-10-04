// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

public sealed class RebaseModel
{
    public string? Host { get; set; }

    public int Port { get; set; }

    public string? Password { get; set; }

    public bool UseSsl { get; set; }

    public DateOnly? Since { get; set; }

    public static RebaseModel Stored()
    {
        return new RebaseModel
        {
            Host = "smtp.stored.test",
            Port = 587,
            Password = "already stored",
            UseSsl = true,
            Since = new DateOnly(2026, 4, 8),
        };
    }
}
