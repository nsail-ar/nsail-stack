// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;
using NSail.Problems;
using NSail.Storage.Providers;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace NSail.Storage.S3;

// Any store that speaks the S3 REST API — AWS, Cloudflare R2, MinIO, Backblaze — reached
// path-style (endpoint / container / key), which is the one addressing form all of them serve.
public class S3StorageProvider : IStorageProvider
{
    public const string ProviderType = "s3";

    public const string ClientName = "NSail.Storage.S3";

    // R2 and MinIO have no regions but the signature has a region slot, and "auto" is what R2
    // documents for it. An install that names its own region overrides this.
    const string DefaultRegion = "auto";

    static readonly XNamespace S3Namespace = "http://s3.amazonaws.com/doc/2006-03-01/";

    readonly IHttpClientFactory _clients;

    public S3StorageProvider(IHttpClientFactory clients)
    {
        _clients = clients;
    }

    public string Type
    {
        get { return ProviderType; }
    }

    public string DisplayName
    {
        get { return "S3-compatible"; }
    }

    // Material Symbols cloud, carried here rather than named from NsIcons: a kit's server
    // assembly does not reference the component library.
    public Glyph Icon
    {
        get
        {
            return new("""<g transform="translate(0,24) scale(0.025)"><path d="M240-192q-80 0-136-56T48-384q0-76 52-131.5T227-576q23-85 92.5-138.5T480-768q103 0 179 69.5T744-528q70 0 119 49t49 119q0 70-49 119t-119 49H240Zm0-72h504q40 0 68-28t28-68q0-40-28-68t-68-28h-66l-6-65q-7-74-62-124.5T480-696q-64 0-115 38.5T297-556l-14 49-51 3q-48 3-80 37.5T120-384q0 50 35 85t85 35Zm240-216Z"/></g>""");
        }
    }

    public bool IsRemote
    {
        get { return true; }
    }

    public async Task Put(StorageTarget target, string key, Stream content, string? contentType, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // This provider writes with a single PUT, not S3's multipart upload, so the length has
        // to be known before the first byte leaves and the object caps at 5 GiB. A seekable
        // stream states it and flows through untouched; anything else is refused here rather
        // than half-written at the far end.
        if (!content.CanSeek)
        {
            throw new BusinessException(BusinessProblem.RuleViolation(
                "ContentNotSeekable",
                "An S3 target needs the length of what it is given, so the content must be a seekable stream."));
        }

        using var request = new HttpRequestMessage(HttpMethod.Put, Address(target, key))
        {
            Content = new StreamContent(content),
        };

        request.Content.Headers.ContentLength = content.Length - content.Position;

        if (contentType is { Length: > 0 })
        {
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }

        using var response = await Send(request, target, await PayloadHash(target, content, cancelToken), cancelToken);

        await Ensure(response, cancelToken);
    }

    public async Task<ObjectContent?> Get(StorageTarget target, string key, CancellationToken cancelToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Address(target, key));

        // The body is handed to the caller as an open read, so the response must not be
        // buffered before this method returns.
        var response = await Send(request, target, S3Signature.EmptyPayload, cancelToken, HttpCompletionOption.ResponseHeadersRead);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            response.Dispose();

            return null;
        }

        try
        {
            await Ensure(response, cancelToken);
        }
        catch
        {
            response.Dispose();

            throw;
        }

        return new ObjectContent
        {
            Content = await response.Content.ReadAsStreamAsync(cancelToken),
            Length = response.Content.Headers.ContentLength ?? -1,
            ContentType = response.Content.Headers.ContentType?.MediaType,
        };
    }

    public async Task<IReadOnlyList<StoredObject>> List(StorageTarget target, string? prefix, CancellationToken cancelToken = default)
    {
        var objects = new List<StoredObject>();
        var token = default(string);

        // A container answers at most a thousand keys per call, so the continuation is followed
        // rather than the first page passed off as the whole listing.
        do
        {
            var query = "?list-type=2";

            if (prefix is { Length: > 0 })
            {
                query += $"&prefix={Uri.EscapeDataString(prefix)}";
            }

            if (token is { Length: > 0 })
            {
                query += $"&continuation-token={Uri.EscapeDataString(token)}";
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, Container(target) + query);
            using var response = await Send(request, target, S3Signature.EmptyPayload, cancelToken);

            await Ensure(response, cancelToken);

            var document = XDocument.Parse(await response.Content.ReadAsStringAsync(cancelToken));

            objects.AddRange(document.Root!
                .Elements(S3Namespace + "Contents")
                .Select(entry => new StoredObject
                {
                    Key = (string)entry.Element(S3Namespace + "Key")!,
                    Length = (long?)entry.Element(S3Namespace + "Size") ?? 0,
                    ModifiedAt = (DateTimeOffset?)entry.Element(S3Namespace + "LastModified") ?? default,
                }));

            token = (bool?)document.Root.Element(S3Namespace + "IsTruncated") == true
                ? (string?)document.Root.Element(S3Namespace + "NextContinuationToken")
                : null;
        }
        while (token is { Length: > 0 });

        return objects;
    }

    public async Task Delete(StorageTarget target, string key, CancellationToken cancelToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, Address(target, key));
        using var response = await Send(request, target, S3Signature.EmptyPayload, cancelToken);

        // A delete of what is not there is what the caller asked for either way.
        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return;
        }

        await Ensure(response, cancelToken);
    }

    // UNSIGNED-PAYLOAD leans on TLS to bind the signature to the body it travels with; an
    // endpoint the administrator pointed at plain http gets none of that from the transport, so
    // the payload is hashed instead — the one extra pass a seekable stream can afford. Put has
    // already required CanSeek, so the position rewinds to where it found it.
    static async Task<string> PayloadHash(StorageTarget target, Stream content, CancellationToken cancelToken)
    {
        if (IsSecure(target))
        {
            return S3Signature.UnsignedPayload;
        }

        var start = content.Position;
        var hash = await SHA256.HashDataAsync(content, cancelToken);

        content.Position = start;

        return Convert.ToHexStringLower(hash);
    }

    static bool IsSecure(StorageTarget target)
    {
        return target.Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    Task<HttpResponseMessage> Send(
        HttpRequestMessage request,
        StorageTarget target,
        string payloadHash,
        CancellationToken cancelToken,
        HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
    {
        S3Signature.Apply(
            request,
            target.AccessKey,
            target.SecretKey,
            target.Region is { Length: > 0 } ? target.Region : DefaultRegion,
            payloadHash,
            DateTimeOffset.UtcNow);

        return _clients.CreateClient(ClientName).SendAsync(request, completion, cancelToken);
    }

    static async Task Ensure(HttpResponseMessage response, CancellationToken cancelToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // The store's own XML refusal says which of the dozen ways a target can be wrong this
        // one is (no such bucket, bad signature, access denied); the status code alone does not.
        var body = await response.Content.ReadAsStringAsync(cancelToken);

        throw new BusinessException(BusinessProblem.RuleViolation(
            "StorageRefused",
            $"The storage service answered {(int)response.StatusCode} {response.ReasonPhrase}: {Describe(body)}",
            new Dictionary<string, string>
            {
                ["status"] = $"{(int)response.StatusCode} {response.ReasonPhrase}",
                ["detail"] = Describe(body),
            }));
    }

    static string Describe(string body)
    {
        try
        {
            var error = XDocument.Parse(body).Root;

            return $"{(string?)error?.Element("Code")} {(string?)error?.Element("Message")}".Trim();
        }
        catch (System.Xml.XmlException)
        {
            return body.Length > 200 ? body[..200] : body;
        }
    }

    static string Container(StorageTarget target)
    {
        if (target.Endpoint is not { Length: > 0 })
        {
            throw new BusinessException(BusinessProblem.RuleViolation(
                "StorageEndpointRequired",
                nameof(StorageTarget.Endpoint),
                "An S3 target needs the address of the service it lives at."));
        }

        return $"{target.Endpoint.TrimEnd('/')}/{Uri.EscapeDataString(target.Container)}";
    }

    static string Address(StorageTarget target, string key)
    {
        // The key's own slashes are its folders and stay literal; everything else in a segment
        // is escaped, which is also what the canonical request the signature covers reads back.
        var path = string.Join('/', key.Split('/').Select(Uri.EscapeDataString));

        return $"{Container(target)}/{path}";
    }
}
