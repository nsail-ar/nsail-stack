// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Serialization;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NSail.Messaging.Http;

public sealed class HttpRequestMessageBuilder
{
    readonly HttpRequestMessage _message = new();
    readonly StringBuilder _query = new(64);
    
    string? _path;

    public HttpRequestMessageBuilder Method(HttpMethod method)
    {
        _message.Method = method;
        return this;
    }

    public HttpRequestMessageBuilder Path(string path)
    {
        _path = path;
        return this;
    }

    /// <summary>A null value drops the parameter: an unset filter is an absent query string,
    /// not "null".</summary>
    public HttpRequestMessageBuilder AddQuery(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(name) || value is null)
        {
            return this;
        }

        AppendQuery(name, value);
        return this;
    }

    public HttpRequestMessageBuilder AddQuery<T>(string name, T value)
    {
        if (string.IsNullOrWhiteSpace(name) || value is null)
        {
            return this;
        }

        AppendQuery(
            name,
            Convert.ToString(value, CultureInfo.InvariantCulture)!);

        return this;
    }

    public HttpRequestMessageBuilder AddQuery<T>(
        string name,
        IEnumerable<T>? values)
    {
        if (string.IsNullOrWhiteSpace(name) || values is null)
        {
            return this;
        }

        foreach (var value in values)
        {
            if (value is null)
            {
                continue;
            }

            AddQuery(name, value);
        }

        return this;
    }

    void AppendQuery(string name, string value)
    {
        _query.Append(_query.Length == 0 ? '?' : '&');
        _query.Append(Uri.EscapeDataString(name));
        _query.Append('=');
        _query.Append(Uri.EscapeDataString(value));
    }

    public HttpRequestMessageBuilder AddHeader(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return this;
        }

        _message.Headers.TryAddWithoutValidation(name, value);
        return this;
    }

    public HttpRequestMessageBuilder AddJsonContent<T>(
        T value,
        JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.Serialize(value, options ?? JsonOptions.Wire);

        _message.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        return this;
    }

    public HttpRequestMessage Build()
    {
        _message.RequestUri = new Uri(
            _path + _query.ToString(),
            UriKind.RelativeOrAbsolute);

        return _message;
    }
}