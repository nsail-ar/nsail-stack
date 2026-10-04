// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Text;

namespace NSail.Components;

public sealed class NsClassBuilder
{
    readonly StringBuilder _sb = new();
    bool _hasAny;

    public NsClassBuilder() { }

    public NsClassBuilder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        Append(value);
    }

    public NsClassBuilder Add(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return this;

        Append(value);
        return this;
    }

    public NsClassBuilder Add(string value, bool when)
    {
        return when ? Add(value) : this;
    }

    void Append(string value)
    {
        var span = value.AsSpan().Trim();

        if (span.IsEmpty)
            return;

        if (_hasAny)
            _sb.Append(' ');

        _sb.Append(span);
        _hasAny = true;
    }

    public override string ToString()
    {
        return _sb.ToString();
    }
}