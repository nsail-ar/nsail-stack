// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Annotations;

/// <summary>
/// Declares the HTTP contract of a message. Properties bind automatically:
/// path {tokens} from the route, the rest from the query string on Get/Delete
/// and from the JSON body on Post/Put/Patch (override with [AsRoute]/[AsQuery]/[AsHeader]).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HttpAttribute : Attribute
{
    public HttpAttribute(Method method, string path)
    {
        Method = method;
        Path = path;
    }

    public Method Method { get; }

    public string Path { get; }
}

public enum Method
{
    Get = 0,
    Post = 1,
    Put = 2,
    Delete = 3,
    Patch = 4,
    Head = 5,
    Options = 6
}