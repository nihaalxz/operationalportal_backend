using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Infrastructure.Persistence;
using TexasOperationalPortal.Infrastructure.Services;

namespace TexasOperationalPortal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OperationalConnection");

        services.AddHttpContextAccessor();

        services.AddDbContext<OperationalDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: System.TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });
        });

        services.AddScoped<IOperationalDbContext>(provider => provider.GetRequiredService<OperationalDbContext>());
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAssignmentCacheService, AssignmentCacheService>();

        // Register Typed HTTP client for Sales Portal internal API integration
        services.AddHttpClient<ISalesOperationsClient, SalesOperationsClient>((provider, client) =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            var baseAddress = config["SalesPortal:BaseAddress"] ?? "http://localhost:5003";
            client.BaseAddress = new System.Uri(baseAddress);
        });

        return services;
    }
}
