using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Linq;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Infrastructure.Persistence;

namespace TexasOperationalPortal.IntegrationTests;

public class OperationalApiTestFactory : WebApplicationFactory<Program>
{
    public Mock<ISalesOperationsClient> SalesClientMock { get; } = new();

    public string ConnectionString { get; }

    public OperationalApiTestFactory()
    {
        ConnectionString = "Server=localhost\\SQLEXPRESS;Database=TexasOperationalPortal_HTTP_Tests;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureTestServices(services =>
        {
            // Remove real DbContext
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<OperationalDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            services.AddDbContext<OperationalDbContext>(options =>
            {
                options.UseSqlServer(ConnectionString);
                options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            // Replace ISalesOperationsClient with mock
            var salesClientDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ISalesOperationsClient));
            if (salesClientDescriptor != null) services.Remove(salesClientDescriptor);
            services.AddSingleton(SalesClientMock.Object);
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OperationalDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await DbInitializer.SeedAsync(db, true);
    }
}
