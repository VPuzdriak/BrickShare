using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BrickShare.Auth;

public static class AuthenticationExtensions
{
    private const string BearerPrefix = "Bearer ";
    private static readonly JwtSecurityTokenHandler TokenReader = new();

    public static IServiceCollection AddBrickShareAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var customerAuthority = configuration["Authentication:Customer:Authority"];

        services.AddAuthentication(Schemes.Token)
            .AddPolicyScheme(Schemes.Token, "Staff or customer token", options =>
            {
                options.ForwardDefaultSelector = context =>
                    SelectScheme(context.Request.Headers.Authorization.ToString(), customerAuthority);
            })
            .AddJwtBearer(Schemes.Staff, options =>
            {
                options.Authority = configuration["Authentication:Staff:Authority"];
                options.Audience = configuration["Authentication:Staff:Audience"];
                options.MapInboundClaims = false;
                options.TokenValidationParameters.RoleClaimType = "roles";
            })
            .AddJwtBearer(Schemes.Customer, options =>
            {
                options.Authority = customerAuthority;
                options.Audience = configuration["Authentication:Customer:Audience"];
                options.MapInboundClaims = false;
                options.TokenValidationParameters.RoleClaimType = "roles";
                options.Events = new JwtBearerEvents { OnTokenValidated = AddCustomerRole };
            });

        return services;
    }

    private static string SelectScheme(string authorization, string? customerAuthority)
    {
        var issuer = ReadUnverifiedIssuer(authorization);

        if (issuer is not null && issuer == customerAuthority)
        {
            return Schemes.Customer;
        }

        return Schemes.Staff;
    }

    private static string? ReadUnverifiedIssuer(string authorization)
    {
        if (!authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorization[BearerPrefix.Length..];

        return TokenReader.CanReadToken(token) ? TokenReader.ReadJwtToken(token).Issuer : null;
    }

    private static Task AddCustomerRole(TokenValidatedContext context)
    {
        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        identity.AddClaim(new Claim(identity.RoleClaimType, "Customer"));

        return Task.CompletedTask;
    }
}