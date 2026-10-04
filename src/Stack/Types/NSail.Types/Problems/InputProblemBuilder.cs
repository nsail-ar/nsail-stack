// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Linq.Expressions;

namespace NSail.Problems;

public sealed class InputProblemBuilder<TModel>
{
    readonly List<Issue> _issues = [];

    public InputProblemBuilder<TModel> Required(string field)
    {
        AddRequired(field);
        return this;
    }

    public InputProblemBuilder<TModel> Required<TValue>(Expression<Func<TModel, TValue>> field)
    {
        AddRequired(Name(field));
        return this;
    }

    public InputProblemBuilder<TModel> InvalidFormat(string field)
    {
        AddInvalidFormat(field);
        return this;
    }

    public InputProblemBuilder<TModel> InvalidFormat<TValue>(Expression<Func<TModel, TValue>> field)
    {
        AddInvalidFormat(Name(field));
        return this;
    }

    public InputProblemBuilder<TModel> OutOfRange(string field)
    {
        AddOutOfRange(field);
        return this;
    }

    public InputProblemBuilder<TModel> OutOfRange<TValue>(Expression<Func<TModel, TValue>> field)
    {
        AddOutOfRange(Name(field));
        return this;
    }

    public InputProblemBuilder<TModel> OutOfRange<TValue>(Expression<Func<TModel, TValue>> field, object from, object to)
    {
        AddOutOfRange(Name(field), from, to);
        return this;
    }

    public InputProblemBuilder<TModel> MaxLength(string field, int max)
    {
        AddMaxLength(field, max);
        return this;
    }

    public InputProblemBuilder<TModel> MaxLength<TValue>(Expression<Func<TModel, TValue>> field, int max)
    {
        AddMaxLength(Name(field), max);
        return this;
    }

    public static implicit operator Problem(InputProblemBuilder<TModel> builder)
    {
        return builder.Build();
    }

    public static implicit operator Exception(InputProblemBuilder<TModel> builder)
    {
        return new BusinessException(builder.Build());
    }

    public Problem Build()
    {
        return new(
            code: "InvalidModel",
            title: "One or more fields contain invalid data",
            issues: _issues.ToArray(),
            status: 400);
    }

    public void AddRequired(string field)
    {
        _issues.Add(new(
            code: "Required",
            message: $"The field {field} is required",
            source: field,
            arguments: new Dictionary<string, string> { ["field"] = field }));
    }

    public void AddInvalidFormat(string field)
    {
        _issues.Add(new(
            code: "InvalidFormat",
            message: $"The field {field} has an invalid format",
            source: field,
            arguments: new Dictionary<string, string> { ["field"] = field }));
    }

    public void AddOutOfRange(string field)
    {
        _issues.Add(new(
            code: "OutOfRange",
            message: $"The field {field} is out of range",
            source: field,
            arguments: new Dictionary<string, string> { ["field"] = field }));
    }

    public void AddOutOfRange(string field, object from, object to)
    {
        _issues.Add(new(
            code: "OutOfRange",
            message: $"The field {field} must be between {from} and {to}",
            source: field,
            arguments: new Dictionary<string, string>
            {
                ["field"] = field,
                ["from"] = from?.ToString() ?? string.Empty,
                ["to"] = to?.ToString() ?? string.Empty
            }));
    }

    /// <summary>A rule that has no dedicated code — the message is the only description.</summary>
    public void AddInvalid(string field, string message)
    {
        _issues.Add(new(
            code: "Invalid",
            message: message,
            source: field,
            arguments: new Dictionary<string, string> { ["field"] = field }));
    }

    /// <summary>A rule with its OWN code — several rules that would otherwise collide on one
    /// field's generic "Invalid" ("Problems.Invalid") translate independently instead
    /// ("Problems.{code}"), the same derived-key mechanism every other typed refusal uses.</summary>
    public void AddInvalid(string code, string field, string message, IReadOnlyDictionary<string, string>? arguments = null)
    {
        var combined = arguments is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(arguments);

        combined["field"] = field;

        _issues.Add(new(
            code: code,
            message: message,
            source: field,
            arguments: combined));
    }

    public void AddMaxLength(string field, int max)
    {
        _issues.Add(new(
            code: "MaxLength",
            message: $"The field {field} exceeds the maximum length of {max}",
            source: field,
            arguments: new Dictionary<string, string>
            {
                ["field"] = field,
                ["max"] = max.ToString()
            }));
    }

    static string Name<TValue>(Expression<Func<TModel, TValue>> expression)
    {
        return expression.Body switch
        {
            MemberExpression member => member.Member.Name,
            UnaryExpression { Operand: MemberExpression member } => member.Member.Name,
            _ => throw new ArgumentException("Expression must reference a property.", nameof(expression))
        };
    }
}
