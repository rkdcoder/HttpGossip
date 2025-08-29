using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace HttpGossip
{
    public sealed class HttpGossipLogger
    {
        private readonly HttpGossipMiddleware _inner;
        private readonly RequestDelegate _noopNext;

        public HttpGossipLogger(IServiceProvider services)
        {
            _inner = services.GetRequiredService<HttpGossipMiddleware>();
            _noopNext = _ => Task.CompletedTask;
        }

        public Task InvokeAsync(HttpContext context) => _inner.InvokeAsync(context, _noopNext);
    }
}