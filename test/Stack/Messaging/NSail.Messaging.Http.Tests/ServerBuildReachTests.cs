// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using NSail.Builds;
using NSail.Messaging.Runtime.Context;
using System.Net;

namespace NSail.Messaging.Http.Tests;

// The client half of nsail#1452: what the server answered with reaches the app from under every
// client, including a name nobody configured, and including the responses a sender never returns
// — a refusal leaves through HandleError, and it is exactly the answer a stale client gets.
public sealed class ServerBuildReachTests
{
    sealed class Ping : IMessage;

    sealed class PingSender : HttpSender<Ping>
    {
        public PingSender(IHttpClientFactory httpClientFactory, MessageContextAccessor deliveries)
            : base(httpClientFactory, deliveries)
        {
        }

        protected override string Name
        {
            get { return "NeverConfigured"; }
        }

        protected override HttpRequestMessage BuildMessage(Ping message)
        {
            return CreateBuilder()
                .Method(HttpMethod.Get)
                .Path("api/ping")
                .Build();
        }
    }

    sealed class Answering : HttpMessageHandler
    {
        // A refusal answers a Problem, which is what HandleError reads before it throws: the
        // point of the failing case is that the read happens under all of that.
        const string Refusal = """{"code":"Boom","title":"Boom","issues":[],"status":500}""";

        readonly string? _version;
        readonly HttpStatusCode _status;

        public Answering(string? version, HttpStatusCode status = HttpStatusCode.OK)
        {
            _version = version;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_status == HttpStatusCode.OK ? "{}" : Refusal),
            };

            if (_version is not null)
            {
                response.Headers.TryAddWithoutValidation(WireHeaders.Version, _version);
            }

            return Task.FromResult(response);
        }
    }

    sealed class Origin : IUrlResolver
    {
        public string Resolve(string urlTemplate)
        {
            return "https://localhost/";
        }
    }

    // The real registration, over a client name that appears in no configuration: what the far
    // side answered with is a property of talking to it, not of any one client's options.
    static ServiceProvider Serving(HttpMessageHandler answering)
    {
        var services = new ServiceCollection();

        services.AddHttpClients(new ConfigurationBuilder().Build(), new Origin());

        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                builder.PrimaryHandler = answering;
            });
        });

        return services.BuildServiceProvider();
    }

    static async Task<ServerBuild> Answered(string? version, HttpStatusCode status = HttpStatusCode.OK)
    {
        await using var provider = Serving(new Answering(version, status));

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("NeverConfigured");

        using var response = await client.GetAsync("api/ping");

        return provider.GetRequiredService<ServerBuild>();
    }

    [Fact]
    public async Task AResponseFromAnotherBuildMovesIt()
    {
        var build = await Answered("1.2.3");

        Assert.True(build.Moved);
    }

    [Fact]
    public async Task AResponseFromThisBuildDoesNot()
    {
        var build = await Answered(BuildVersion.Current);

        Assert.False(build.Moved);
    }

    [Fact]
    public async Task AResponseCarryingNoVersionDoesNot()
    {
        var build = await Answered(null);

        Assert.False(build.Moved);
    }

    // A refusal is a response too. The sender never returns it — HandleError throws over it —
    // so the read is under the client, where the status is nobody's business.
    [Fact]
    public async Task ARefusalCarriesItAsWell()
    {
        var build = await Answered("1.2.3", HttpStatusCode.InternalServerError);

        Assert.True(build.Moved);
    }

    // The same refusal through the real sender: the caller gets its BusinessException and the
    // app still learns the server moved.
    [Fact]
    public async Task ASendThatFailsStillLearnsTheServerMoved()
    {
        await using var provider = Serving(new Answering("1.2.3", HttpStatusCode.InternalServerError));

        var sender = new PingSender(
            provider.GetRequiredService<IHttpClientFactory>(),
            new MessageContextAccessor());

        await Assert.ThrowsAsync<BusinessException>(() => sender.Send(new Ping()));

        Assert.True(provider.GetRequiredService<ServerBuild>().Moved);
    }
}
