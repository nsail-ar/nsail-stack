// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Storage.S3;

namespace NSail.Storage.Tests;

// AWS publishes worked examples of Signature Version 4 for S3, down to the finished
// Authorization header. Checking against one is what makes a hand-written signer honest: every
// other test of it would only prove that it agrees with itself.
public sealed class S3SignatureTests
{
    const string AccessKey = "AKIAIOSFODNN7EXAMPLE";

    const string SecretKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

    static readonly DateTimeOffset Signed = new(2013, 5, 24, 0, 0, 0, TimeSpan.Zero);

    // "Example: GET Object" — a ranged read of examplebucket/test.txt.
    [Fact]
    public void MatchesTheDocumentedGetObjectExample()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://examplebucket.s3.amazonaws.com/test.txt");

        request.Headers.TryAddWithoutValidation("Range", "bytes=0-9");

        Assert.Equal(
            "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request, "
            + "SignedHeaders=host;range;x-amz-content-sha256;x-amz-date, "
            + "Signature=f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41",
            S3Signature.Authorization(request, AccessKey, SecretKey, "us-east-1", S3Signature.EmptyPayload, Signed));
    }

    // "Example: GET Bucket Lifecycle" — the same request with a query string to canonicalize.
    [Fact]
    public void MatchesTheDocumentedLifecycleExample()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://examplebucket.s3.amazonaws.com/?lifecycle");

        Assert.Equal(
            "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request, "
            + "SignedHeaders=host;x-amz-content-sha256;x-amz-date, "
            + "Signature=fea454ca298b7da1c68078a5d1bdbfbbe0d65c699e0f91ac7a200a0136783543",
            S3Signature.Authorization(request, AccessKey, SecretKey, "us-east-1", S3Signature.EmptyPayload, Signed));
    }

    [Fact]
    public void SignsTheHeadersItPutsOnTheRequest()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://store.example.com/bucket/backups/a.zip");

        S3Signature.Apply(request, AccessKey, SecretKey, "auto", S3Signature.UnsignedPayload, Signed);

        Assert.Equal(S3Signature.UnsignedPayload, request.Headers.GetValues("x-amz-content-sha256").Single());
        Assert.Equal("20130524T000000Z", request.Headers.GetValues("x-amz-date").Single());
        Assert.Contains("SignedHeaders=host;x-amz-content-sha256;x-amz-date", request.Headers.GetValues("Authorization").Single(), StringComparison.Ordinal);
    }
}
