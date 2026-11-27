using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.IdentityModel.Tokens;

namespace BrickShare.Customers.Api.Tests;

public static class CustomerTestTokens
{
    public const string StaffIssuer = "https://login.microsoftonline.com/00000000-0000-0000-0000-0000000000a1/v2.0";
    public const string CustomerIssuer = "https://brickshare.ciamlogin.com/00000000-0000-0000-0000-0000000000c1/v2.0";
    public const string ClientId = "00000000-0000-0000-0000-0000000000b1";

    public static SymmetricSecurityKey SigningKey { get; } =
        new(Encoding.UTF8.GetBytes("customer-tests-signing-key-with-at-least-32-bytes"));

    public static string Customer(Guid oid, string email, string name) =>
        Create(CustomerIssuer, new Claim("oid", oid.ToString()), new Claim("email", email), new Claim("name", name));

    public static string Staff(string role) =>
        Create(StaffIssuer, new Claim("oid", Guid.CreateVersion7().ToString()), new Claim("roles", role));

    private static string Create(string issuer, params Claim[] claims)
    {
        var credentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: ClientId,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}