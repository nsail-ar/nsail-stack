// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using System.Globalization;
using System.Reflection;

namespace NSail.Components;

public sealed class RouteTable
{
    static readonly IReadOnlyDictionary<string, object?> s_emptyParameters =
        new Dictionary<string, object?>();

    readonly Assembly _appAssembly;
    readonly IReadOnlyList<Assembly> _additionalAssemblies;
    readonly Dictionary<Type, string[]> _templates = new();

    public RouteTable(Assembly appAssembly, IReadOnlyList<Assembly> additionalAssemblies)
    {
        ArgumentNullException.ThrowIfNull(appAssembly);
        ArgumentNullException.ThrowIfNull(additionalAssemblies);

        _appAssembly = appAssembly;
        _additionalAssemblies = additionalAssemblies;
        IndexRoutes();
    }

    // The empty string is the app ROOT, not a missing argument: NavigationManager.ToBaseRelativePath
    // answers "" for the base address, so every caller that reads where it is standing — NsTitleBar
    // deriving its icon, NsSurface routing a surface's own address — hands exactly that whenever the
    // user is on a page routed at "/". NsNavMenu.FindActive already reads the empty path as the
    // root's own address and the two have to agree. The root needs no case of its own below: an
    // empty path and the "/" template both split to zero segments, so TryMatch pairs them like any
    // other route.
    public RouteData? Match(string route)
    {
        ArgumentNullException.ThrowIfNull(route);

        var path = NormalizePath(route);

        RouteData? bestMatch = null;
        var bestScore = int.MinValue;

        foreach (var pageType in GetRouteableComponents())
        {
            foreach (var attribute in pageType.GetCustomAttributes<RouteAttribute>(inherit: false))
            {
                if (!TryMatch(path, pageType, attribute.Template, out var routeData, out var score))
                {
                    continue;
                }

                if (score > bestScore)
                {
                    bestMatch = routeData;
                    bestScore = score;
                }
            }
        }

        return bestMatch;
    }

    public string GetUrl<T>(object? parameters = null) where T : IComponent
    {
        return GetUrl(typeof(T), parameters);
    }

    /// <summary>Parameters as an anonymous object or an IReadOnlyDictionary&lt;string, object?&gt;.</summary>
    public string GetUrl(Type componentType, object? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(componentType);

        if (!_templates.TryGetValue(componentType, out var templates) || templates.Length == 0)
        {
            throw new InvalidOperationException($"No route found for component '{componentType.Name}'.");
        }

        var values = ToDictionary(parameters);
        var template = SelectTemplate(templates, values);

        return BuildUrl(template, values);
    }

    void IndexRoutes()
    {
        foreach (var type in GetRouteableComponents())
        {
            if (_templates.ContainsKey(type))
            {
                continue;
            }

            var templates = type.GetCustomAttributes<RouteAttribute>(inherit: false)
                .Select(a => a.Template)
                .ToArray();

            if (templates.Length > 0)
            {
                _templates[type] = templates;
            }
        }
    }

    static string SelectTemplate(string[] templates, IReadOnlyDictionary<string, object?>? parameters)
    {
        if (templates.Length == 1)
        {
            return templates[0];
        }

        // Prefer the template that binds the most parameters, considering only
        // templates whose tokens are all satisfied by the provided parameters.
        string? best = null;
        var bestTokenCount = -1;

        foreach (var template in templates)
        {
            var tokens = GetTemplateTokens(template);

            var satisfied = tokens.All(token =>
                parameters is not null && parameters.Keys.Any(key =>
                    string.Equals(key, token, StringComparison.OrdinalIgnoreCase)));

            if (satisfied && tokens.Count > bestTokenCount)
            {
                best = template;
                bestTokenCount = tokens.Count;
            }
        }

        return best ?? templates[0];
    }

    static List<string> GetTemplateTokens(string template)
    {
        var tokens = new List<string>();
        var open = template.IndexOf('{');

        while (open >= 0)
        {
            var close = template.IndexOf('}', open + 1);

            if (close < 0)
            {
                break;
            }

            var name = template[(open + 1)..close].TrimStart('*').Split(':')[0];
            tokens.Add(name);

            open = template.IndexOf('{', close + 1);
        }

        return tokens;
    }

    // A parameter that names no {token} in the template is not a route value — it is a
    // deep-link filter (PartiesPage's RoleIds, opened pre-filtered from Optical's
    // "Patients" menu entry) and travels as a query string instead of being dropped.
    static string BuildUrl(string template, IReadOnlyDictionary<string, object?>? parameters)
    {
        var url = template.StartsWith('/') ? template : "/" + template;

        if (parameters is null || parameters.Count == 0)
        {
            return url;
        }

        var tokens = GetTemplateTokens(template);
        var query = new List<string>();

        foreach (var (key, value) in parameters)
        {
            if (tokens.Any(token => string.Equals(token, key, StringComparison.OrdinalIgnoreCase)))
            {
                url = ReplaceParameter(url, key, Uri.EscapeDataString(SurfaceQuery.Format(value)));
                continue;
            }

            AppendQuery(query, key, value);
        }

        return query.Count == 0 ? url : $"{url}?{string.Join('&', query)}";
    }

    static void AppendQuery(List<string> query, string key, object? value)
    {
        if (value is null)
        {
            return;
        }

        if (value is string || !value.GetType().IsAssignableTo(typeof(System.Collections.IEnumerable)))
        {
            query.Add($"{key}={Uri.EscapeDataString(SurfaceQuery.Format(value))}");
            return;
        }

        foreach (var item in (System.Collections.IEnumerable)value)
        {
            if (item is not null)
            {
                query.Add($"{key}={Uri.EscapeDataString(SurfaceQuery.Format(item))}");
            }
        }
    }

    static string ReplaceParameter(string template, string key, string value)
    {
        var open = template.IndexOf('{');

        while (open >= 0)
        {
            var close = template.IndexOf('}', open + 1);

            if (close < 0)
            {
                break;
            }

            var segment = template[open..(close + 1)];
            var name = segment.TrimStart('{').TrimEnd('}').Split(':')[0];

            if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
            {
                return template.Replace(segment, value, StringComparison.Ordinal);
            }

            open = template.IndexOf('{', close + 1);
        }

        return template;
    }

    static IReadOnlyDictionary<string, object?> ToDictionary(object? parameters)
    {
        if (parameters is null)
        {
            return s_emptyParameters;
        }

        if (parameters is IReadOnlyDictionary<string, object?> dictionary)
        {
            return dictionary;
        }

        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in parameters.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            values[property.Name] = property.GetValue(parameters);
        }

        return values;
    }

    static string NormalizePath(string route)
    {
        var path = route.Split('?')[0].Split('#')[0];

        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        return path;
    }

    IEnumerable<Type> GetRouteableComponents()
    {
        foreach (var type in _appAssembly.ExportedTypes)
        {
            if (IsRouteable(type))
            {
                yield return type;
            }
        }

        foreach (var assembly in _additionalAssemblies)
        {
            if (assembly == _appAssembly)
            {
                continue;
            }

            foreach (var type in assembly.ExportedTypes)
            {
                if (IsRouteable(type))
                {
                    yield return type;
                }
            }
        }
    }

    static bool IsRouteable(Type type)
    {
        return typeof(IComponent).IsAssignableFrom(type)
        && type.IsDefined(typeof(RouteAttribute), inherit: false)
        && !type.IsDefined(typeof(ExcludeFromInteractiveRoutingAttribute), inherit: false);
    }

    static bool TryMatch(
        string path,
        Type pageType,
        string template,
        out RouteData routeData,
        out int score)
    {
        routeData = null!;
        score = 0;

        var pathSegments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var templateSegments = template.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var catchAllIndex = -1;

        for (var i = 0; i < templateSegments.Length; i++)
        {
            if (templateSegments[i].StartsWith("{*", StringComparison.Ordinal))
            {
                catchAllIndex = i;
                break;
            }
        }

        if (catchAllIndex >= 0)
        {
            if (pathSegments.Length < catchAllIndex)
            {
                return false;
            }
        }
        else if (pathSegments.Length != templateSegments.Length)
        {
            return false;
        }

        for (var i = 0; i < templateSegments.Length; i++)
        {
            var templateSegment = templateSegments[i];

            if (catchAllIndex >= 0 && i == catchAllIndex)
            {
                var parameterName = templateSegment[2..^1];
                values[parameterName] = string.Join('/', pathSegments.Skip(i));
                score = (score * 10) + 1;
                break;
            }

            if (pathSegments.Length <= i)
            {
                return false;
            }

            var pathSegment = pathSegments[i];

            if (templateSegment.StartsWith('{') && templateSegment.EndsWith('}'))
            {
                var parts = templateSegment[1..^1].Split(':');

                if (!TryConvert(pathSegment, parts.Length > 1 ? parts[1] : null, out var value))
                {
                    return false;
                }

                values[parts[0]] = value;
                score = (score * 10) + 1;
            }
            else if (!string.Equals(templateSegment, pathSegment, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            else
            {
                score = (score * 10) + 2;
            }
        }

        routeData = new RouteData(pageType, values);
        return true;
    }

    // Blazor's route constraint syntax ("{id:guid}"): the constraint both filters the
    // match (bad value → no route) and types the value, so pages declare typed
    // [Parameter]s instead of parsing strings by hand. Unknown constraints pass through
    // as strings.
    static bool TryConvert(string segment, string? constraint, out object? value)
    {
        var decoded = Uri.UnescapeDataString(segment);

        switch (constraint)
        {
            case null:
                value = decoded;
                return true;

            case "guid" when Guid.TryParse(decoded, out var guid):
                value = guid;
                return true;

            // Unspecified kind, deliberately: a route carries a wall clock (the hour a
            // practice means), and reading it as local or UTC would move it.
            case "datetime" when DateTime.TryParse(
                decoded,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var moment):
                value = moment;
                return true;

            case "int" when int.TryParse(decoded, out var number):
                value = number;
                return true;

            case "long" when long.TryParse(decoded, out var longNumber):
                value = longNumber;
                return true;

            case "bool" when bool.TryParse(decoded, out var flag):
                value = flag;
                return true;

            case "guid" or "int" or "long" or "bool" or "datetime":
                value = null;
                return false;

            default:
                value = decoded;
                return true;
        }
    }
}
