using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Contracts.Auth;
using TexasOperationalPortal.Contracts.Common;
using TexasOperationalPortal.Contracts.Bookings;
using TexasOperationalPortal.Contracts.Dashboard;
using TexasOperationalPortal.Contracts.Staff;
using TexasOperationalPortal.Domain.Entities;
using TexasOperationalPortal.Infrastructure.Persistence;
using Xunit;

namespace TexasOperationalPortal.IntegrationTests;

public class LoginAndDashboardTests : IClassFixture<OperationalApiTestFactory>, IAsyncLifetime
{
    private readonly OperationalApiTestFactory _factory;
    private readonly HttpClient _client;

    public LoginAndDashboardTests(OperationalApiTestFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();
        _factory.SalesClientMock.Reset();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private string? GetRefreshTokenCookie(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            foreach (var value in values)
            {
                if (value.StartsWith("refresh_token="))
                {
                    var endIdx = value.IndexOf(';');
                    return value.Substring("refresh_token=".Length, endIdx - "refresh_token=".Length);
                }
            }
        }
        return null;
    }

    [Fact]
    public async Task LocalLogin_Success_Returns_Token_And_SetsCookie()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@texasconnect.site", "Admin@123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authResult = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(authResult);
        Assert.False(string.IsNullOrWhiteSpace(authResult.AccessToken));
        Assert.Equal("admin@texasconnect.site", authResult.Email);

        var cookie = GetRefreshTokenCookie(response);
        Assert.NotNull(cookie);
        Assert.False(string.IsNullOrWhiteSpace(cookie));
    }

    [Fact]
    public async Task LocalLogin_Failure_Returns_Generic401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@texasconnect.site", "WrongPass123"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LocalLogin_DisabledUser_Returns_Generic401()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OperationalDbContext>();
            var admin = await db.Users.FirstAsync(u => u.Email == "admin@texasconnect.site");
            admin.IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@texasconnect.site", "Admin@123"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginRateLimit_Returns_429()
    {
        using var customFactory = new OperationalApiTestFactory();
        using var client = customFactory.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();

        for (int i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@texasconnect.site", "WrongPass123"));
        }

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@texasconnect.site", "WrongPass123"));
        Assert.Equal((HttpStatusCode)429, response.StatusCode);
    }

    [Fact]
    public async Task RefreshTokenLifecycle_RotatesSuccessfully_And_DetectsReuse()
    {
        // 1. Login to get initial tokens
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@texasconnect.site", "Admin@123"));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginResult);
        var firstRefreshToken = GetRefreshTokenCookie(loginResponse);
        Assert.NotNull(firstRefreshToken);

        // 2. Perform first refresh
        var refreshReq1 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshReq1.Headers.Add("Cookie", $"refresh_token={firstRefreshToken}");
        
        var refreshResponse1 = await _client.SendAsync(refreshReq1);
        Assert.Equal(HttpStatusCode.OK, refreshResponse1.StatusCode);
        var refreshResult1 = await refreshResponse1.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(refreshResult1);
        Assert.False(string.IsNullOrWhiteSpace(refreshResult1.AccessToken));
        
        var secondRefreshToken = GetRefreshTokenCookie(refreshResponse1);
        Assert.NotNull(secondRefreshToken);
        Assert.NotEqual(firstRefreshToken, secondRefreshToken);

        // 3. Attempt to REUSE the first refresh token (Breach detection check)
        var refreshReq2 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshReq2.Headers.Add("Cookie", $"refresh_token={firstRefreshToken}");
        
        var refreshResponse2 = await _client.SendAsync(refreshReq2);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse2.StatusCode);

        // Verify that the reuse breach invalidated the second refresh token as well!
        var refreshReq3 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshReq3.Headers.Add("Cookie", $"refresh_token={secondRefreshToken}");
        
        var refreshResponse3 = await _client.SendAsync(refreshReq3);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse3.StatusCode);
    }

    [Fact]
    public async Task Logout_InvalidatesRefreshToken()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@texasconnect.site", "Admin@123"));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginResult);
        var refreshToken = GetRefreshTokenCookie(loginResponse);
        Assert.NotNull(refreshToken);

        // Call Logout with authorization and refresh token cookie
        var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.AccessToken);
        logoutReq.Headers.Add("Cookie", $"refresh_token={refreshToken}");

        var logoutResponse = await _client.SendAsync(logoutReq);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        // Attempting refresh should fail now
        var refreshReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshReq.Headers.Add("Cookie", $"refresh_token={refreshToken}");
        
        var refreshResponse = await _client.SendAsync(refreshReq);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task GetMe_Returns_CurrentUserProfile()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@texasconnect.site", "Admin@123"));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginResult);

        var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.AccessToken);

        var meResponse = await _client.SendAsync(meReq);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var profile = await meResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(profile);
        Assert.Equal("admin@texasconnect.site", profile.Email);
        Assert.Equal("Admin", profile.Name);
    }

    [Fact]
    public async Task DisabledUser_IsBlocked_Immediately_ByMiddleware()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@texasconnect.site", "Admin@123"));
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginResult);

        // Deactivate user in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OperationalDbContext>();
            var admin = await db.Users.FirstAsync(u => u.Email == "admin@texasconnect.site");
            admin.IsActive = false;
            await db.SaveChangesAsync();
        }

        // Try calling protected endpoint with previously active token
        var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.AccessToken);

        var meResponse = await _client.SendAsync(meReq);
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    public async Task StaffSync_IdempotentUpsert_And_PermissionFilters()
    {
        var syncKey = "730f173df8e40c2284961c1b7e1333610f4158e2a2954fc3c2cfd908b43ffb5b";
        var staffSyncPayload = new StaffSyncRequest(
            Email: "synced.ops@texasconnect.site",
            Name: "Synced Ops Agent",
            UserType: "Operations",
            Permissions: new List<string> { "visas", "malicious_unallowed_permission" },
            PasswordHash: BCrypt.Net.BCrypt.HashPassword("Synced@Pass123"),
            IsActive: true
        );

        // 1. Sync User (Insert scenario)
        var syncReq = new HttpRequestMessage(HttpMethod.Post, "/api/internal/staff-sync");
        syncReq.Headers.Add("X-SYNC-KEY", syncKey);
        syncReq.Content = JsonContent.Create(staffSyncPayload);

        var syncResponse = await _client.SendAsync(syncReq);
        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);

        // Verify insertion and permission filtering
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OperationalDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "synced.ops@texasconnect.site");
            Assert.NotNull(user);
            Assert.Equal("Synced Ops Agent", user.Name);
            Assert.Equal(UserType.Operations, user.UserType);
            Assert.True(user.IsActive);

            var permissions = System.Text.Json.JsonSerializer.Deserialize<List<string>>(user.PermissionsJson ?? "[]") ?? new List<string>();
            Assert.Contains("visas", permissions);
            Assert.DoesNotContain("malicious_unallowed_permission", permissions);
        }

        // 2. Perform local login with synced user
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("synced.ops@texasconnect.site", "Synced@Pass123"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginResult);
        Assert.Equal("Synced Ops Agent", loginResult.Name);

        // 3. Update active status via sync (Update scenario)
        var updatePayload = staffSyncPayload with { IsActive = false, Name = "Synced Ops Updated" };
        var syncReq2 = new HttpRequestMessage(HttpMethod.Post, "/api/internal/staff-sync");
        syncReq2.Headers.Add("X-SYNC-KEY", syncKey);
        syncReq2.Content = JsonContent.Create(updatePayload);

        var syncResponse2 = await _client.SendAsync(syncReq2);
        Assert.Equal(HttpStatusCode.OK, syncResponse2.StatusCode);

        // Verify status update
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OperationalDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == "synced.ops@texasconnect.site");
            Assert.Equal("Synced Ops Updated", user.Name);
            Assert.False(user.IsActive);
        }
    }

    [Fact]
    public async Task StaffSync_InvalidKey_Returns_401()
    {
        var payload = new StaffSyncRequest("bad.ops@texasconnect.site", "Bad Agent", "Operations", new(), "hash", true);
        var syncReq = new HttpRequestMessage(HttpMethod.Post, "/api/internal/staff-sync");
        syncReq.Headers.Add("X-SYNC-KEY", "malicious_key_123");
        syncReq.Content = JsonContent.Create(payload);

        var response = await _client.SendAsync(syncReq);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StaffSync_InvalidUserType_Returns_400()
    {
        var payload = new StaffSyncRequest("bad.ops@texasconnect.site", "Bad Agent", "InvalidRole", new(), "hash", true);
        var syncReq = new HttpRequestMessage(HttpMethod.Post, "/api/internal/staff-sync");
        syncReq.Headers.Add("X-SYNC-KEY", "730f173df8e40c2284961c1b7e1333610f4158e2a2954fc3c2cfd908b43ffb5b");
        syncReq.Content = JsonContent.Create(payload);

        var response = await _client.SendAsync(syncReq);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
