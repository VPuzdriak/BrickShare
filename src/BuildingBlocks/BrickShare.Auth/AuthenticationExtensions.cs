using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BrickShare.Auth;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddBrickShareAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var authentication = services.AddAuthentication();

        if (environment.IsDevelopment() && configuration.GetValue<bool>("Authentication:UseDevelopmentIssuer"))
        {
            authentication.AddJwtBearer();
        }

        return services;
    }
}