using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Application.Common.Options;
using TexasOperationalPortal.Infrastructure.BackgroundServices;
using TexasOperationalPortal.Infrastructure.Hubs;
using TexasOperationalPortal.Infrastructure.Notifications;

namespace TexasOperationalPortal.Api.Extensions;

public static class SignalRServiceExtensions
{
    public static IServiceCollection AddRealtimeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SignalROptions>(configuration.GetSection(SignalROptions.SectionName));
        services.Configure<WorkerOptions>(configuration.GetSection(WorkerOptions.SectionName));
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));

        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<INotificationPublisher, NotificationPublisher>();
        services.AddHostedService<OutboxNotificationWorker>();

        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.HandshakeTimeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }

    public static void ConfigureJwtBearerQueryStringForSignalR(this JwtBearerEvents events)
    {
        events.OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        };
    }

    public static WebApplication MapRealtimeHubs(this WebApplication app)
    {
        app.MapHub<TexasSyncHub>("/hubs/texas-sync");
        return app;
    }
}
