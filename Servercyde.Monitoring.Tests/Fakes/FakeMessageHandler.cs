using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace Servercyde.Monitoring.Tests.Fakes;

public static class HttpTestExtensions
{
    public static IServiceCollection AddHttpInterceptor(
        this IServiceCollection services,
        out FakeHttpMessageHandler interceptor
    )
    {
        var factory = new FakeHttpMessageHandler.Factory();
        interceptor = factory.Handler;
        services.AddSingleton<IHttpMessageHandlerFactory>(factory);
        return services;
    }
}

public class FakeHttpMessageHandler : DelegatingHandler
{
    public class Factory : IHttpMessageHandlerFactory
    {
        public readonly FakeHttpMessageHandler Handler = new();

        public HttpMessageHandler CreateHandler(string name)
        {
            return Handler;
        }
    }

    readonly List<Func<HttpRequestMessage, HttpResponseMessage?>> _handlers = [];
    public List<HttpRequestMessage> Requests { get; } = [];

    public string LastRequestUri => Requests.LastOrDefault()?.RequestUri?.AbsoluteUri ?? string.Empty;

    public string[] RequestUris => [.. Requests.Select(x => x.RequestUri?.AbsoluteUri ?? string.Empty)];

    public void AddHandler(Func<HttpRequestMessage, HttpResponseMessage?> fn)
    {
        _handlers.Add(fn);
    }

    public void RespondWithOK(string content)
    {
        AddHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        });
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(() =>
        {
            Requests.Add(request);
            var firstResponse = _handlers
                .Select(handler => handler(request))
                .FirstOrDefault(response => response != null);
            return firstResponse ?? new HttpResponseMessage(HttpStatusCode.OK);
        });
    }

    public void AddHandlers(
        params (string uri, string response)[] handlers)
    {
        foreach (var handler in handlers)
        {
            var fn = new Func<HttpRequestMessage, HttpResponseMessage?>(request =>
            {
                var uri = request.RequestUri?.ToString() ?? string.Empty;
                return uri.Contains(handler.uri)
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(handler.response)
                    }
                    : null;
            });
            AddHandler(fn);
        }
    }
}

