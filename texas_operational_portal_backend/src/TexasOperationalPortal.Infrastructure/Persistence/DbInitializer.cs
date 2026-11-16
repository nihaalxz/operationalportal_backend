using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Domain.Entities;

namespace TexasOperationalPortal.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAsync(IOperationalDbContext context, bool isDevelopmentOrTest)
    {
        if (isDevelopmentOrTest)
        {
            var hasDevAdmin = await context.Users.AnyAsync(u => u.Email == "admin@texasconnect.site");
            
            if (!hasDevAdmin)
            {
                var admin = new OperationalUser
                {
                    Name = "Admin",
                    Email = "admin@texasconnect.site",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    UserType = UserType.Admin,
                    IsActive = true,
                    PermissionsJson = "[\"visas\", \"vfs\", \"holidays\", \"manage_users\"]"
                };
                
                context.Users.Add(admin);
                await context.SaveChangesAsync(default);
            }
        }

        try
        {
            var relationalDb = context as DbContext;
            if (relationalDb != null)
            {
                await relationalDb.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkloadLimits')
                    BEGIN
                        CREATE TABLE [WorkloadLimits] (
                            [Id] bigint IDENTITY(1,1) NOT NULL,
                            [EntityType] nvarchar(50) NOT NULL,
                            [MaxActiveLimit] int NOT NULL DEFAULT 3,
                            [UserId] bigint NULL,
                            [UpdatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                            [UpdatedByUserId] bigint NULL,
                            CONSTRAINT [PK_WorkloadLimits] PRIMARY KEY ([Id])
                        );
                    END
                ");
            }
        }
        catch { }

        if (!await context.WorkloadLimits.AnyAsync())
        {
            context.WorkloadLimits.AddRange(
                new WorkloadLimit { EntityType = "VISA", MaxActiveLimit = 3, UpdatedAt = DateTime.UtcNow },
                new WorkloadLimit { EntityType = "VFS", MaxActiveLimit = 3, UpdatedAt = DateTime.UtcNow },
                new WorkloadLimit { EntityType = "HOLIDAY", MaxActiveLimit = 3, UpdatedAt = DateTime.UtcNow }
            );
            await context.SaveChangesAsync(default);
        }

        // Production bootstrap flow
        var bootstrapEmail = Environment.GetEnvironmentVariable("ADMIN_BOOTSTRAP_EMAIL");
        var bootstrapPassword = Environment.GetEnvironmentVariable("ADMIN_BOOTSTRAP_PASSWORD");

        if (!string.IsNullOrWhiteSpace(bootstrapEmail) && !string.IsNullOrWhiteSpace(bootstrapPassword))
        {
            if (bootstrapPassword.Equals("Admin@123", StringComparison.OrdinalIgnoreCase) || bootstrapPassword.Length < 8)
            {
                throw new InvalidOperationException("CRITICAL: Insecure bootstrap credentials are forbidden in production environments.");
            }

            var normalizedEmail = bootstrapEmail.Trim().ToLowerInvariant();
            var hasAdmin = await context.Users.AnyAsync(u => u.Email == normalizedEmail);
            if (!hasAdmin)
            {
                var admin = new OperationalUser
                {
                    Name = "Bootstrap Administrator",
                    Email = normalizedEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(bootstrapPassword),
                    UserType = UserType.Admin,
                    IsActive = true,
                    PermissionsJson = "[\"visas\", \"vfs\", \"holidays\", \"manage_users\"]"
                };
                
                context.Users.Add(admin);
                await context.SaveChangesAsync(default);
            }
        }
    }
}
