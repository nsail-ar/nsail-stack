// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Linq.Expressions;

namespace NSail.Components;

public static class ForExpression
{
    /// <summary>The member a binding expression points at: model type plus member name
    /// (the model type is the expression's, not the declaring one, so derived models key
    /// on themselves). Callers turn it into a key via MetadataProvider.KeyFor.</summary>
    public static (Type Type, string Member)? GetTarget(LambdaExpression? expression)
    {
        if (expression is null)
        {
            return null;
        }

        var body = expression.Body;

        if (body is UnaryExpression { Operand: var unwrapped })
        {
            body = unwrapped;
        }

        if (body is not MemberExpression member)
        {
            return null;
        }

        var type = member.Expression?.Type ?? member.Member.DeclaringType;

        return type is null ? null : (type, member.Member.Name);
    }

    public static string? GetMemberName(LambdaExpression? expression)
    {
        if (expression is null)
        {
            return null;
        }

        var body = expression.Body;

        if (body is UnaryExpression { Operand: var operand })
        {
            body = operand;
        }

        if (body is MemberExpression member)
        {
            return member.Member.Name;
        }

        return null;
    }

}
