using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using TexasOperationalPortal.Domain.Entities;
using TexasOperationalPortal.Infrastructure.Services;
using Xunit;

namespace TexasOperationalPortal.UnitTests;

public class TokenServiceTests
{
    [Fact]
    public void GenerateTokens_Should_Create_ValidJwt_With_CorrectClaims()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?> {
            {"Jwt:Key", "tx_ops_super_secret_key_999_must_be_long_enough"},
            {"Jwt:Issuer", "texas_operational_portal"},
            {"Jwt:Audience", "texas_operational_portal_clients"},
            {"Jwt:ExpiryHours", "2"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var tokenService = new TokenService(configuration);
        var user = new OperationalUser
        {
            Id = 42,
            Name = "Alice Operations",
            Email = "alice@connect.com",
            UserType = UserType.Operations
        };
        var permissions = new List<string> { "visas", "vfs" };

        // Act
        var result = tokenService.GenerateTokens(user, permissions);

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));

        // Parse token and verify
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(result.AccessToken);

        Assert.Equal("texas_operational_portal", jwtToken.Issuer);
        Assert.Contains("texas_operational_portal_clients", jwtToken.Audiences);

        // Verify claims
        Assert.Equal("42", jwtToken.Subject);
        
        var claims = jwtToken.Claims;
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Operations");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Name && c.Value == "Alice Operations");
        
        var permissionClaims = jwtToken.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();
        Assert.Contains("visas", permissionClaims);
        Assert.Contains("vfs", permissionClaims);
    }
}
