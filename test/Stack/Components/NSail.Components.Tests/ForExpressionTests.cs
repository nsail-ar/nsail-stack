// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Linq.Expressions;
using NSail.Components;
using NSail.Testing.Models;

namespace NSail.Components.Tests;

public sealed class ForExpressionTests
{
    [Fact]
    public void GetTarget_returns_the_model_type_and_member()
    {
        var model = new TestModel();
        Expression<Func<object?>> expression = () => model.Name;

        var target = ForExpression.GetTarget(expression);

        Assert.NotNull(target);
        Assert.Equal(typeof(TestModel), target.Value.Type);
        Assert.Equal("Name", target.Value.Member);
    }

    [Fact]
    public void GetTarget_unwraps_value_type_boxing()
    {
        var model = new TestModel();
        Expression<Func<object?>> expression = () => model.Age;

        var target = ForExpression.GetTarget(expression);

        Assert.NotNull(target);
        Assert.Equal(typeof(TestModel), target.Value.Type);
        Assert.Equal("Age", target.Value.Member);
    }

    [Fact]
    public void GetTarget_returns_null_for_null_or_non_member_expressions()
    {
        Expression<Func<object?>> constant = () => "literal";

        Assert.Null(ForExpression.GetTarget(null));
        Assert.Null(ForExpression.GetTarget(constant));
    }

    [Fact]
    public void GetMemberName_returns_the_member()
    {
        var model = new TestModel();
        Expression<Func<object?>> expression = () => model.Name;

        Assert.Equal("Name", ForExpression.GetMemberName(expression));
    }
}
