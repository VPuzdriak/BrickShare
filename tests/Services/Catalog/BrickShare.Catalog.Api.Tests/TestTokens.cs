using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.IdentityModel.Tokens;

namespace BrickShare.Catalog.Api.Tests;

public static class TestTokens
{
    public const string Issuer = "https://brickshare.test";
    public const string Audience = "brickshare-catalog-tests";

    public static SymmetricSecurityKey SigningKey { get; } =
        new(Encoding.UTF8.GetBytes("catalog-tests-signing-key-with-at-least-32-bytes"));

    public static string Create(string role)
    {
        var credentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims:
            [
                new Claim("oid", Guid.CreateVersion7().ToString()),
                new Claim("role", role)
            ],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}