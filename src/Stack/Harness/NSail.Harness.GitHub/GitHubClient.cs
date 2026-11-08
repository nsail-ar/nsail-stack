// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NSail.Harness.GitHub;

// REST and GraphQL over one HttpClient. Every read either answers whole or throws:
// a half read must never reach the planner as an empty queue.
public sealed partial class GitHubClient(HttpClient http)
{
    public static void Configure(HttpClient http, string token)
    {
        http.BaseAddress = new Uri("https://api.github.com/");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("harness");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        http.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task<JsonElement> Get(string path, CancellationToken ct = default)
    {
        using var response = await http.GetAsync(path, ct);
        return await Read(response, ct);
    }

    // Follows the Link header; `select` picks the array out of each page (the page itself
    // when it is an array, `workflow_runs` or `runners` when it is wrapped). A next link left at
    // `maxPages` throws: a prefix is never returned as the whole.
    public async Task<List<JsonElement>> GetAll(string path, Func<JsonElement, JsonElement>? select = null, int maxPages = 20, CancellationToken ct = default)
    {
        var all = new List<JsonElement>();
        string? next = path;
        for (var page = 0; next is not null && page < maxPages; page++)
        {
            using var response = await http.GetAsync(next, ct);
            var body = await Read(response, ct);
            var items = select is null ? body : select(body);
            all.AddRange(items.EnumerateArray());
            next = NextLink(response);
        }
        if (next is not null)
        {
            throw new GitHubPageLimitException(path, maxPages);
        }
        return all;
    }

    public async Task<JsonElement> GraphQl(string query, object? variables = null, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { query, variables });
        using var response = await http.PostAsync("graphql", new StringContent(payload, Encoding.UTF8, "application/json"), ct);
        var body = await Read(response, ct);
        if (body.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
        {
            throw new GitHubException(HttpStatusCode.OK, $"GraphQL: {errors[0].GetProperty("message").GetString()}");
        }
        return body.GetProperty("data");
    }

    public async Task<JsonElement> Send(HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }
        using var response = await http.SendAsync(request, ct);
        return await Read(response, ct);
    }

    // A file's bytes to an absolute upload address (uploads.github.com), its JSON answer back.
    public async Task<JsonElement> Upload(string address, Stream content, string contentType, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, address) { Content = new StreamContent(content) };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        using var response = await http.SendAsync(request, ct);
        return await Read(response, ct);
    }

    static async Task<JsonElement> Read(HttpResponseMessage response, CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new GitHubException(response.StatusCode, $"{(int)response.StatusCode} {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery}: {Truncate(text)}", RateLimitRemaining(response));
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            return default;
        }
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    static long? RateLimitRemaining(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("x-ratelimit-remaining", out var values)
            && long.TryParse(values.FirstOrDefault(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var left) ? left : null;
    }

    static string? NextLink(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values)) return null;
        var match = NextRel().Match(string.Join(",", values));
        return match.Success ? match.Groups[1].Value : null;
    }

    static string Truncate(string s)
    {
        return s.Length <= 300 ? s : s[..300];
    }

    [GeneratedRegex("<([^>]+)>;\\s*rel=\"next\"")]
    private static partial Regex NextRel();
}
