using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;

namespace TexasOperationalPortal.Api.Middleware;

public sealed class UserActiveCheckMiddleware : IMiddleware
{
    private readonly IOperationalDbContext _db;
    private readonly IMemoryCache _cache;

    private sealed record UserCacheEntry(bool IsActive, string Name, string? PermissionsJson);

    public UserActiveCheckMiddleware(IOperationalDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var subClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? context.User.FindFirst(ClaimTypes.Name)?.Value;

            if (long.TryParse(subClaim, out var userId))
            {
                var cacheKey = $"user-active-check-{userId}";
                if (!_cache.TryGetValue<UserCacheEntry>(cacheKey, out var cacheEntry))
                {
                    var user = await _db.Users
                        .AsNoTracking()
                        .Where(u => u.Id == userId)
                        .Select(u => new { u.IsActive, u.Name, u.PermissionsJson })
                        .FirstOrDefaultAsync(context.RequestAborted);

                    if (user == null)
                    {
                        cacheEntry = new UserCacheEntry(false, "Unknown", null);
                    }
                    else
                    {
                        cacheEntry = new UserCacheEntry(user.IsActive, user.Name ?? "Operator", user.PermissionsJson);
                    }

                    _cache.Set(cacheKey, cacheEntry, TimeSpan.FromMinutes(1));
                }

                if (!cacheEntry.IsActive)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";

                    var problem = new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "Unauthorized",
                        Detail = "Your account is deactivated or no longer exists. Access denied."
                    };

                    await context.Response.WriteAsJsonAsync(problem);
                    return;
                }

                // Dynamically hydrate current live permissions from DB into ClaimsIdentity
                if (!string.IsNullOrWhiteSpace(cacheEntry.PermissionsJson) && context.User.Identity is ClaimsIdentity identity)
                {
                    try
                    {
                        var perms = JsonSerializer.Deserialize<List<string>>(cacheEntry.PermissionsJson);
                        if (perms != null)
                        {
                            foreach (var p in perms)
                            {
                                if (!identity.HasClaim("permission", p))
                                {
                                    identity.AddClaim(new Claim("permission", p));
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        await next(context);
    }
}
