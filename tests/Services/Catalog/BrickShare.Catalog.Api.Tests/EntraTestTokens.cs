using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.IdentityModel.Tokens;

namespace BrickShare.Catalog.Api.Tests;

public static class EntraTestTokens
{
    public const string StaffIssuer = "https://login.microsoftonline.com/00000000-0000-0000-0000-0000000000a1/v2.0";
    public const string CustomerIssuer = "https://brickshare.ciamlogin.com/00000000-0000-0000-0000-0000000000c1/v2.0";
    public const string ClientId = "00000000-0000-0000-0000-0000000000b1";

    public static SymmetricSecurityKey SigningKey { get; } =
        new(Encoding.UTF8.GetBytes("entra-tests-signing-key-with-at-least-32-bytes"));

    public static string Staff(string role) =>
        Create(StaffIssuer, ClientId, new Claim("roles", role));

    public static string Customer() =>
        Create(CustomerIssuer, ClientId);

    public static string Create(string issuer, string audience, params Claim[] claims)
    {
        var credentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: [new Claim("oid", Guid.CreateVersion7().ToString()), .. claims],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}