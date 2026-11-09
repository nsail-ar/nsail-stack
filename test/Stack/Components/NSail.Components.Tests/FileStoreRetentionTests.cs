// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using NSail.Components.Tests.Fixtures;
using System.Net;

namespace NSail.Components.Tests;

/// <summary>What a store and the guide reader keep for the scope: names, never bytes; an
/// answer, never a transient failure.</summary>
public sealed class FileStoreRetentionTests
{
    [Fact]
    public async Task A_hundred_saved_files_retain_their_name_and_no_bytes()
    {
        var store = new RecordingFileStore();
        var ids = new List<Guid>();

        for (var i = 0; i < 100; i++)
        {
            ids.Add(await store.Save(new FileContent($"f{i}.png", "image/png", new byte[1024])));
        }

        Assert.All(ids, id =>
        {
            var kept = store.Get(id);

            Assert.NotNull(kept);
            Assert.Empty(kept.Content);
        });
        Assert.Equal("f7.png", store.GetName(ids[7]));
    }

    [Fact]
    public async Task A_transient_failure_is_retried_and_a_404_is_cached()
    {
        var handler = new SequenceHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        var reader = new GuideReader(new HttpClient(handler), new StubNavigation());
        var chapter = new GuideItem { Name = "A", File = "a.md" };

        Assert.Null(await reader.Read(chapter));
        Assert.NotNull(await reader.Read(chapter));

        var calls = handler.Calls;

        Assert.NotNull(await reader.Read(chapter));
        Assert.Equal(calls, handler.Calls);

        var missing = new SequenceHandler(HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.OK);
        var other = new GuideReader(new HttpClient(missing), new StubNavigation());

        Assert.Null(await other.Read(chapter));
        Assert.Null(await other.Read(chapter));
    }

    sealed class SequenceHandler(params HttpStatusCode[] answers) : HttpMessageHandler
    {
        int _next;

        public int Calls => _next;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var status = answers[Math.Min(_next++, answers.Length - 1)];

            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("# Title") });
        }
    }

    sealed class StubNavigation : NavigationManager
    {
        public StubNavigation()
        {
            Initialize("http://localhost/", "http://localhost/");
        }
    }
}
