using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Domain.Entities;

namespace TexasOperationalPortal.Infrastructure.Services;

public sealed class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public TokenResult GenerateTokens(OperationalUser user, List<string> permissions)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.UserType.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var perm in permissions)
        {
            claims.Add(new Claim("permission", perm));
        }

        var jwtKey = _config["Jwt:Key"] ?? "tx_ops_super_secret_key_999_must_be_long_enough";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(_config.GetValue<int>("Jwt:ExpiryHours", 8));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "texas_operational_portal",
            audience: _config["Jwt:Audience"] ?? "texas_operational_portal_clients",
            claims: claims,
            expires: expiry,
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        
        var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        return new TokenResult(accessToken, refreshToken);
    }
}
