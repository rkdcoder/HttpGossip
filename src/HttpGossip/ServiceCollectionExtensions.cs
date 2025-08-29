using HttpGossip.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HttpGossip
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHttpGossip(this IServiceCollection services, Action<HttpGossipOptions> configure)
        {
            services.Configure(configure);

            services.AddSingleton<LogQueue>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<HttpGossipOptions>>().Value;
                opts.Validate();
                return new LogQueue(opts.QueueCapacity);
            });

            // Expose public writer interface using the same singleton
            services.AddSingleton<ILogQueueWriter>(sp => sp.GetRequiredService<LogQueue>());

            services.AddSingleton<ILogRepository>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<HttpGossipOptions>>().Value;
                return new LogRepository(opts);
            });

            services.AddHostedService<LogWriterHostedService>();
            services.AddTransient<HttpGossipMiddleware>();

            services.AddSingleton<HttpGossipLogger>();

            return services;
        }

        public static IApplicationBuilder UseHttpGossip(this IApplicationBuilder app)
            => app.UseMiddleware<HttpGossipMiddleware>();
    }
}