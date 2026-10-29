// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Problems;
using NSail.Storage.Providers;
using NSail.Storage.S3;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace NSail.Storage.Tests;

// What the provider puts on the wire, offline — the MetaTokensTests shape (a stub
// HttpMessageHandler behind a one-client IHttpClientFactory). What needs a real bucket is
// whether a store ACCEPTS the signature; what the provider BUILDS — the address, the payload
// hash it declares, the body that follows it, the pages it walks, the refusal it reads back —
// owes no credential and is answered here. The signature's own arithmetic is S3SignatureTests'
// job, against AWS's published vectors.
public sealed class S3TransportTests
{
    const string Secret = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

    static readonly byte[] Payload = Encoding.UTF8.GetBytes("the archive nsail#326 will write");

    // storage.md's first documented upload shape: over TLS the transport carries the integrity,
    // so the body is read once and the signature says so.
    [Fact]
    public async Task PutOverHttpsDeclaresAnUnsignedPayload()
    {
        var transport = new Transport();

        using (var content = new MemoryStream(Payload))
        {
            await Provider(transport).Put(Target("https://store.example.com"), "backups/a.zip", content, "application/zip");
        }

        Assert.Equal(S3Signature.UnsignedPayload, transport.Last.Header("x-amz-content-sha256"));
    }

    // The other documented shape, and the one round 1 found asserted by a comment and held by
    // nothing: a plaintext endpoint gets no integrity from the transport, so the signature has
    // to bind the body itself. The body arriving whole is the rewind, proven rather than read.
    [Fact]
    public async Task PutOverAPlaintextEndpointHashesTheBodyAndStillSendsIt()
    {
        var transport = new Transport();

        using (var content = new MemoryStream(Payload))
        {
            await Provider(transport).Put(Target("http://minio.local:9000"), "backups/a.zip", content, "application/zip");
        }

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Payload)), transport.Last.Header("x-amz-content-sha256"));
        Assert.Equal(Payload, transport.Last.Body);
        Assert.Equal(Payload.Length, transport.Last.Body.Length);
    }

    // Path-style addressing is what makes one provider cover AWS, R2, MinIO and Backblaze: the
    // key's own slashes stay its folders and everything else in a segment is escaped, which is
    // also what the canonical request the signature covers reads back.
    [Fact]
    public async Task PutAddressesTheObjectPathStyleAndEscapesTheKey()
    {
        var transport = new Transport();

        using (var content = new MemoryStream(Payload))
        {
            await Provider(transport).Put(Target("https://store.example.com/"), "backups/a b.zip", content, null);
        }

        Assert.Equal(HttpMethod.Put, transport.Last.Method);
        Assert.Equal("https://store.example.com/bucket/backups/a%20b.zip", transport.Last.Url);
    }

    // A single PUT needs the length before the first byte leaves, so a source that cannot state
    // one is refused HERE — not half-written at the far end.
    [Fact]
    public async Task PutRefusesANonSeekableSourceWithoutOpeningASocket()
    {
        var transport = new Transport();

        using var source = new ProbeStream(64, _ => { });

        await Assert.ThrowsAsync<BusinessException>(
            () => Provider(transport).Put(Target("https://store.example.com"), "backups/a.zip", source, null));

        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task GetAnswersNullForAnObjectThatIsNotThere()
    {
        var transport = new Transport(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Null(await Provider(transport).Get(Target("https://store.example.com"), "backups/gone.zip"));
    }

    [Fact]
    public async Task GetHandsBackTheBodyWithWhatTheStoreSaidAboutIt()
    {
        var transport = new Transport(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Payload)
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip") },
            },
        });

        using var stored = await Provider(transport).Get(Target("https://store.example.com"), "backups/a.zip");

        Assert.NotNull(stored);
        Assert.Equal("application/zip", stored.ContentType);
        Assert.Equal(Payload.Length, stored.Length);

        using var readBack = new MemoryStream();
        await stored.Content.CopyToAsync(readBack);

        Assert.Equal(Payload, readBack.ToArray());
    }

    // A container answers at most a thousand keys per call. A listing that stopped at the first
    // page would read as a complete one, which is how a backup sweep silently keeps everything.
    [Fact]
    public async Task ListFollowsTheContinuationToken()
    {
        var transport = new Transport(request => Xml(request.RequestUri!.Query.Contains("continuation-token=tok-2", StringComparison.Ordinal)
            ? Page("backups/b.zip", null)
            : Page("backups/a.zip", "tok-2")));

        var objects = await Provider(transport).List(Target("https://store.example.com"), "backups/");

        Assert.Equal(["backups/a.zip", "backups/b.zip"], objects.Select(stored => stored.Key));
        Assert.Equal(2, transport.Sent.Count);
        Assert.Contains("prefix=backups%2F", transport.Sent[0].Url, StringComparison.Ordinal);
    }

    // A delete of what is not there is what the caller asked for either way.
    [Fact]
    public async Task DeleteTreatsAnObjectThatWasAlreadyGoneAsDone()
    {
        var transport = new Transport(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await Provider(transport).Delete(Target("https://store.example.com"), "backups/gone.zip");

        Assert.Equal(HttpMethod.Delete, transport.Last.Method);
    }

    // The status code alone does not say which of the dozen ways a target can be wrong this one
    // is; the store's own XML does, and it is what the settings screen shows the administrator.
    [Fact]
    public async Task AFailingStatusCarriesTheStoresOwnRefusal()
    {
        var transport = new Transport(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("<Error><Code>NoSuchBucket</Code><Message>The specified bucket does not exist</Message></Error>"),
        });

        var refusal = await Assert.ThrowsAsync<BusinessException>(
            () => Provider(transport).List(Target("https://store.example.com"), null));

        Assert.Contains(refusal.Issues, issue => issue.Message.Contains("NoSuchBucket", StringComparison.Ordinal));
    }

    // The registration, not the wire, but the same failure it prevents: HttpClient's 100-second
    // default covers the whole body, so it decides how large an object may be just as surely as
    // the 5 GiB cap does. Left in a comment it walks back on the next edit and the suite stays
    // green, which is the shape of round 1's finding here.
    [Fact]
    public void TheRegisteredClientCarriesNoWallClockCeiling()
    {
        var services = new ServiceCollection();

        services.AddS3Storage();

        using var client = services.BuildServiceProvider()
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(S3StorageProvider.ClientName);

        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }

    static S3StorageProvider Provider(Transport transport)
    {
        return new S3StorageProvider(transport);
    }

    static StorageTarget Target(string endpoint)
    {
        return new StorageTarget(S3StorageProvider.ProviderType, endpoint, "auto", "bucket", "AKIAIOSFODNN7EXAMPLE", Secret);
    }

    static HttpResponseMessage Xml(string body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
    }

    static string Page(string key, string? next)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/">
              <IsTruncated>{(next is null ? "false" : "true")}</IsTruncated>
              <Contents>
                <Key>{key}</Key>
                <Size>3</Size>
                <LastModified>2026-08-21T10:00:00.000Z</LastModified>
              </Contents>
              {(next is null ? "" : $"<NextContinuationToken>{next}</NextContinuationToken>")}
            </ListBucketResult>
            """;
    }

    // The request is read WHILE it is being sent, because the provider disposes it the moment
    // the call returns and its content stream with it.
    sealed class Transport : HttpMessageHandler, IHttpClientFactory
    {
        readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public Transport()
            : this(_ => new HttpResponseMessage(HttpStatusCode.OK))
        {
        }

        public Transport(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public List<Recorded> Sent { get; } = [];

        public Recorded Last
        {
            get { return Sent[^1]; }
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(this, disposeHandler: false);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sent.Add(new Recorded(
                request.Method,
                request.RequestUri!.AbsoluteUri,
                request.Headers.ToDictionary(header => header.Key.ToLowerInvariant(), header => string.Join(",", header.Value), StringComparer.Ordinal),
                request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken)));

            return _respond(request);
        }
    }

    sealed record Recorded(HttpMethod Method, string Url, Dictionary<string, string> Headers, byte[] Body)
    {
        public string? Header(string name)
        {
            return Headers.TryGetValue(name, out var value) ? value : null;
        }
    }
}
