using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Nuget.Proxy.Logic.Benchmark.Tests;

internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly byte[] _payload;

    public FakeHttpClientFactory(byte[] payload)
    {
        this._payload = payload;
    }

    [SuppressMessage(
        category: "Microsoft.Reliability",
        checkId: "CA2000: Dispose objects before losing scope",
        Justification = "Ownership of the handler transfers to the returned HttpClient, which disposes it"
    )]
    public HttpClient CreateClient(string name)
    {
        return new(new FakeHttpMessageHandler(this._payload));
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly byte[] _payload;

        public FakeHttpMessageHandler(byte[] payload)
        {
            this._payload = payload;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(this._payload) }
            );
        }
    }
}
